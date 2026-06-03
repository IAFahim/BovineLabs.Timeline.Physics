using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    /// <summary>
    /// Applies teleports. 
    /// IMPORTANT INVARIANT: At most one teleport clip may act on a single target entity per tick.
    /// The job uses [NativeDisableParallelForRestriction] on LocalTransformLookup and PhysicsVelocityLookup
    /// for performance (keyed on clip entities, not teleport targets). Two teleport clips resolving
    /// the same EntityToTeleport in one frame will cause a non-deterministic last-writer-wins race
    /// on that entity's transform/velocity. This is by design — enforce the invariant at authoring time.
    /// </summary>
    public partial struct PhysicsTeleportApplySystem : ISystem
    {
        private EntityQuery _query;
        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveTeleport> _activeTeleportHandle;
        private ComponentTypeHandle<PhysicsTeleportState> _teleportStateHandle;

        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<PhysicsVelocity> _physicsVelocityLookup;
        
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;
        private ConditionEventWriter.Lookup _conditionWriters;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsTeleportState>()
                .WithAll<ActiveTeleport>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _activeTeleportHandle = state.GetComponentTypeHandle<ActiveTeleport>(true);
            _teleportStateHandle = state.GetComponentTypeHandle<PhysicsTeleportState>();

            _localTransformLookup = state.GetComponentLookup<LocalTransform>();
            _physicsVelocityLookup = state.GetComponentLookup<PhysicsVelocity>();

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _conditionWriters.Create(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityHandle.Update(ref state);
            _activeTeleportHandle.Update(ref state);
            _teleportStateHandle.Update(ref state);

            _localTransformLookup.Update(ref state);
            _physicsVelocityLookup.Update(ref state);

            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);
            _conditionWriters.Update(ref state);

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            state.Dependency = new TeleportJob
            {
                CollisionWorld = collisionWorld,
                EntityHandle = _entityHandle,
                ActiveTeleportHandle = _activeTeleportHandle,
                TeleportStateHandle = _teleportStateHandle,
                
                LocalTransformLookup = _localTransformLookup,
                PhysicsVelocityLookup = _physicsVelocityLookup,
                
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                ParentLookup = _parentLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ConditionWriters = _conditionWriters
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct TeleportJob : IJobChunk
        {
            [ReadOnly] public CollisionWorld CollisionWorld;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveTeleport> ActiveTeleportHandle;
            public ComponentTypeHandle<PhysicsTeleportState> TeleportStateHandle;

            [NativeDisableParallelForRestriction]
            public ComponentLookup<LocalTransform> LocalTransformLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [NativeDisableParallelForRestriction] public ConditionEventWriter.Lookup ConditionWriters;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveTeleportHandle);
                var teleportStates = chunk.GetNativeArray(ref TeleportStateHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var teleportState = teleportStates[i];
                    if (teleportState.Fired) continue;

                    var selfEntity = entities[i];
                    var config = actives[i].Config;

                    teleportState.Fired = true;
                    teleportStates[i] = teleportState;

                    // 1. Resolve entity to teleport
                    var targetToTeleport = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.EntityToTeleport, config.EntityToTeleportLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    if (targetToTeleport == Entity.Null || !LocalToWorldLookup.TryGetComponent(targetToTeleport, out var teleportLtw))
                        continue;

                    var teleportPos = teleportLtw.Position;
                    var teleportRot = new quaternion(math.orthonormalize(new float3x3(teleportLtw.Value)));

                    // 2. Resolve landing sphere origin (TeleportRelativeTo)
                    var landingEntity = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.TeleportRelativeTo, config.TeleportRelativeToLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    if (!LocalToWorldLookup.TryGetComponent(landingEntity, out var landingLtw))
                        landingLtw = teleportLtw;

                    var landingPos = landingLtw.Position;

                    // 3. Resolve azimuth reference (AzimuthTarget - what azimuth 0° points toward)
                    var azimuthEntity = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.AzimuthTarget, config.AzimuthTargetLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    float3 azimuthPos = landingPos;
                    quaternion azimuthRot = quaternion.identity;
                    if (LocalToWorldLookup.TryGetComponent(azimuthEntity, out var azimuthLtw))
                    {
                        azimuthPos = azimuthLtw.Position;
                        azimuthRot = new quaternion(math.orthonormalize(new float3x3(azimuthLtw.Value)));
                    }

                    // 4. Resolve facing target (FacingTarget - where entity looks after teleport)
                    var facingEntity = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.FacingTarget, config.FacingTargetLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    float3 facingPos = landingPos;
                    quaternion facingRot = azimuthRot;
                    if (facingEntity != Entity.Null &&
                        LocalToWorldLookup.TryGetComponent(facingEntity, out var facingLtw))
                    {
                        facingPos = facingLtw.Position;
                        facingRot = new quaternion(math.orthonormalize(new float3x3(facingLtw.Value)));
                    }

                    var targets = TargetsLookup.TryGetComponent(selfEntity, out var t) ? t : default;

                    // 5. Apply stat multiplier to radius
                    var radiusMultiplier = StatStrengthUtility.Resolve(
                        in config.Strength, selfEntity, targets, LinkSources, Links, StatLookup);
                    var radius = config.Radius * math.max(radiusMultiplier, 0f);

                    // 6. Line of sight check from teleport position to landing position
                    if (config.RequireLineOfSight)
                    {
                        var hasLos = TeleportMath.CheckLineOfSight(
                            in CollisionWorld, teleportPos, landingPos,
                            config.LineOfSightOffset, config.ObstacleMask,
                            targetToTeleport, landingEntity);

                        if (!hasLos)
                        {
                            FireFailure(selfEntity, targetToTeleport, config, targets);
                            continue;
                        }
                    }

                    // 7. Compute reference rotation for candidate generation
                    // The forward of this rotation defines what azimuth 0° points toward.
                    TeleportMath.ResolveReferenceRotation(
                        teleportPos, teleportRot, azimuthPos, azimuthRot,
                        TeleportReferenceFrame.TargetToSelf,
                        out var referenceRotation);

                    // 8. Generate candidates and find valid landing position
                    var maxCandidates = math.clamp(config.MaxCandidates, 1, 64);
                    var foundValid = false;
                    var validPosition = float3.zero;

                    for (var c = 0; c < maxCandidates; c++)
                    {
                        TeleportMath.GenerateCandidate(
                            c, maxCandidates,
                            config.AzimuthCenter, config.AzimuthHalfRange,
                            config.ElevationCenter, config.ElevationHalfRange,
                            referenceRotation, radius, landingPos,
                            out var candidatePos);

                        var clearanceOk = TeleportMath.CheckClearance(
                            in CollisionWorld, candidatePos,
                            config.ClearanceRadius, config.ObstacleMask, targetToTeleport);

                        if (!clearanceOk) continue;

                        if (config.RequireCandidateVisibility)
                        {
                            var candidateLos = TeleportMath.CheckLineOfSight(
                                in CollisionWorld, candidatePos, landingPos,
                                config.LineOfSightOffset, config.ObstacleMask,
                                targetToTeleport, landingEntity);

                            if (!candidateLos) continue;
                        }

                        validPosition = candidatePos;
                        foundValid = true;
                        break;
                    }

                    if (!foundValid)
                    {
                        FireFailure(selfEntity, targetToTeleport, config, targets);
                        continue;
                    }

                    // 9. Compute final facing rotation
                    TeleportMath.ComputeFacingRotation(
                        config.FacingMode, validPosition, facingPos,
                        teleportRot, facingRot,
                        out var finalFacingRot);

                    var worldTransform = LocalTransform.FromPositionRotation(validPosition, finalFacingRot);

                    // 10. Handle parent transforms
                    if (ParentLookup.HasComponent(targetToTeleport))
                    {
                        var parent = ParentLookup[targetToTeleport];
                        if (LocalToWorldLookup.TryGetComponent(parent.Value, out var parentLtw))
                        {
                            var worldMatrix = float4x4.TRS(worldTransform.Position, worldTransform.Rotation, 1f);
                            var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                            worldTransform = LocalTransform.FromMatrix(localMatrix);
                        }
                    }

                    // 11. Apply transform
                    if (LocalTransformLookup.HasComponent(targetToTeleport))
                        LocalTransformLookup[targetToTeleport] = worldTransform;

                    // 12. Reset velocity if configured
                    if (config.ResetVelocity && PhysicsVelocityLookup.HasComponent(targetToTeleport))
                    {
                        var vel = PhysicsVelocityLookup[targetToTeleport];
                        vel.Linear = float3.zero;
                        vel.Angular = float3.zero;
                        PhysicsVelocityLookup[targetToTeleport] = vel;
                    }
                }
            }

            private void FireFailure(Entity selfEntity, Entity targetEntity, in PhysicsTeleportData config,
                in Targets targets)
            {
                if (config.FailureCondition == ConditionKey.Null) return;

                if (!PhysicsTriggerResolution.TryResolveLinkedTarget(
                        config.FailureRouteTo, config.FailureRouteLinkKey,
                        selfEntity, targetEntity, targets, LinkSources, Links,
                        out var routeTarget))
                    return;

                if (ConditionWriters.TryGet(routeTarget, out var writer))
                    writer.Trigger(config.FailureCondition, config.FailureValue);
            }
        }
    }
}