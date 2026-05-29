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
    /// Applies teleports. Invariant: At most one teleport track may act on a single target entity per tick.
    /// Multiple teleports on the same target in the same tick cause non-deterministic last-writer-wins behavior.
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
        private ComponentLookup<Targets> _targetsLookup;
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
            _targetsLookup = state.GetComponentLookup<Targets>(true);
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
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
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

                    var targetToTeleport = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.EntityToTeleport, config.EntityToTeleportLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    if (targetToTeleport == Entity.Null || !LocalToWorldLookup.TryGetComponent(targetToTeleport, out var teleportLtw))
                        continue;

                    var referenceEntity = TeleportMath.ResolveTargetEntity(
                        selfEntity, config.TeleportRelativeTo, config.TeleportRelativeToLinkKey, 
                        TargetsLookup, LinkSources, Links);

                    if (!LocalToWorldLookup.TryGetComponent(referenceEntity, out var referenceLtw))
                        referenceLtw = teleportLtw;

                    var teleportPos = teleportLtw.Position;
                    var referencePos = referenceLtw.Position;
                    var referenceRot = new quaternion(referenceLtw.Value);

                    var targets = TargetsLookup.TryGetComponent(selfEntity, out var t) ? t : default;
                    var radiusMultiplier = StatStrengthUtility.Resolve(
                        in config.Strength, selfEntity, targets, LinkSources, Links, StatLookup);
                    var radius = config.Radius * math.max(radiusMultiplier, 0f);

                    if (config.RequireLineOfSight)
                    {
                        var hasLos = TeleportMath.CheckLineOfSight(
                            in CollisionWorld, teleportPos, referencePos,
                            config.LineOfSightOffset, config.ObstacleMask,
                            targetToTeleport, referenceEntity);

                        if (!hasLos)
                        {
                            FireFailure(selfEntity, targetToTeleport, config, targets);
                            continue;
                        }
                    }

                    TeleportMath.ResolveReferenceRotation(
                        config.ReferenceFrame, teleportPos, referencePos, referenceRot,
                        out var referenceRotation);

                    var maxCandidates = math.clamp(config.MaxCandidates, 1, 64);
                    var foundValid = false;
                    var validPosition = float3.zero;

                    for (var c = 0; c < maxCandidates; c++)
                    {
                        TeleportMath.GenerateCandidate(
                            c, maxCandidates,
                            config.AzimuthCenter, config.AzimuthHalfRange,
                            config.ElevationCenter, config.ElevationHalfRange,
                            referenceRotation, radius, referencePos,
                            out var candidatePos);

                        var clearanceOk = TeleportMath.CheckClearance(
                            in CollisionWorld, candidatePos,
                            config.ClearanceRadius, config.ObstacleMask, targetToTeleport);

                        if (!clearanceOk) continue;

                        if (config.RequireCandidateVisibility)
                        {
                            var candidateLos = TeleportMath.CheckLineOfSight(
                                in CollisionWorld, candidatePos, referencePos,
                                config.LineOfSightOffset, config.ObstacleMask,
                                targetToTeleport, referenceEntity);

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

                    TeleportMath.ComputeFacingRotation(
                        config.FacingMode, validPosition, referencePos,
                        new quaternion(teleportLtw.Value), referenceRot,
                        out var facingRot);

                    var worldTransform = LocalTransform.FromPositionRotation(validPosition, facingRot);

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

                    if (LocalTransformLookup.HasComponent(targetToTeleport))
                        LocalTransformLookup[targetToTeleport] = worldTransform;

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