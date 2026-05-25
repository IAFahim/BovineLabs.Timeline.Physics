using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
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
    public partial struct PhysicsTeleportApplySystem : ISystem
    {
        private EntityQuery _query;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveTeleport> _activeTeleportHandle;
        private ComponentTypeHandle<PhysicsTeleportState> _teleportStateHandle;
        private ComponentTypeHandle<LocalTransform> _localTransformHandle;
        private ComponentTypeHandle<PhysicsVelocity> _physicsVelocityHandle;

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
                .WithAllRW<PhysicsTeleportState, LocalTransform>()
                .WithAll<ActiveTeleport, LocalToWorld>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _activeTeleportHandle = state.GetComponentTypeHandle<ActiveTeleport>(true);
            _teleportStateHandle = state.GetComponentTypeHandle<PhysicsTeleportState>();
            _localTransformHandle = state.GetComponentTypeHandle<LocalTransform>();
            _physicsVelocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();

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
            _localTransformHandle.Update(ref state);
            _physicsVelocityHandle.Update(ref state);
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
                LocalTransformHandle = _localTransformHandle,
                PhysicsVelocityHandle = _physicsVelocityHandle,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                ParentLookup = _parentLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ConditionWriters = _conditionWriters
            }.Schedule(_query, state.Dependency);
        }

        [BurstCompile]
        private struct TeleportJob : IJobChunk
        {
            [ReadOnly] public CollisionWorld CollisionWorld;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveTeleport> ActiveTeleportHandle;
            public ComponentTypeHandle<PhysicsTeleportState> TeleportStateHandle;
            public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
            public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityHandle;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            public ConditionEventWriter.Lookup ConditionWriters;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveTeleportHandle);
                var teleportStates = chunk.GetNativeArray(ref TeleportStateHandle);
                var localTransforms = chunk.GetNativeArray(ref LocalTransformHandle);

                var hasVelocity = chunk.Has(ref PhysicsVelocityHandle);
                var velocities = hasVelocity ? chunk.GetNativeArray(ref PhysicsVelocityHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var teleportState = teleportStates[i];
                    if (teleportState.Fired) continue;

                    var selfEntity = entities[i];
                    var config = actives[i].Config;

                    teleportState.Fired = true;
                    teleportStates[i] = teleportState;

                    if (!LocalToWorldLookup.TryGetComponent(selfEntity, out var selfLtw)) continue;

                    var targetEntity = TeleportMath.ResolveTargetEntity(
                        selfEntity, config, TargetsLookup, LinkSources, Links);

                    if (!LocalToWorldLookup.TryGetComponent(targetEntity, out var targetLtw))
                        targetLtw = selfLtw;

                    var selfPos = selfLtw.Position;
                    var targetPos = targetLtw.Position;
                    var targetRot = new quaternion(targetLtw.Value);

                    var targets = TargetsLookup.HasComponent(selfEntity) ? TargetsLookup[selfEntity] : default;
                    var radiusMultiplier = StatStrengthUtility.Resolve(
                        in config.Strength, selfEntity, targets, LinkSources, Links, StatLookup);
                    var radius = config.Radius * math.max(radiusMultiplier, 0f);

                    if (config.RequireLineOfSight)
                    {
                        var hasLos = TeleportMath.CheckLineOfSight(
                            in CollisionWorld, selfPos, targetPos,
                            config.LineOfSightOffset, config.ObstacleMask,
                            selfEntity, targetEntity);

                        if (!hasLos)
                        {
                            FireFailure(selfEntity, targetEntity, config, targets);
                            continue;
                        }
                    }

                    TeleportMath.ResolveReferenceRotation(
                        config.ReferenceFrame, selfPos, targetPos, targetRot,
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
                            referenceRotation, radius, targetPos,
                            out var candidatePos);

                        var clearanceOk = TeleportMath.CheckClearance(
                            in CollisionWorld, candidatePos,
                            config.ClearanceRadius, config.ObstacleMask, selfEntity);

                        if (!clearanceOk) continue;

                        if (config.RequireCandidateVisibility)
                        {
                            var candidateLos = TeleportMath.CheckLineOfSight(
                                in CollisionWorld, candidatePos, targetPos,
                                config.LineOfSightOffset, config.ObstacleMask,
                                selfEntity, targetEntity);

                            if (!candidateLos) continue;
                        }

                        validPosition = candidatePos;
                        foundValid = true;
                        break;
                    }

                    if (!foundValid)
                    {
                        FireFailure(selfEntity, targetEntity, config, targets);
                        continue;
                    }

                    TeleportMath.ComputeFacingRotation(
                        config.FacingMode, validPosition, targetPos,
                        new quaternion(selfLtw.Value), targetRot,
                        out var facingRot);

                    var worldTransform = LocalTransform.FromPositionRotation(validPosition, facingRot);

                    if (ParentLookup.HasComponent(selfEntity))
                    {
                        var parent = ParentLookup[selfEntity];
                        if (LocalToWorldLookup.TryGetComponent(parent.Value, out var parentLtw))
                        {
                            var worldMatrix = float4x4.TRS(worldTransform.Position, worldTransform.Rotation, 1f);
                            var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                            worldTransform = LocalTransform.FromMatrix(localMatrix);
                        }
                    }

                    localTransforms[i] = worldTransform;

                    if (config.ResetVelocity && hasVelocity)
                    {
                        var vel = velocities[i];
                        vel.Linear = float3.zero;
                        vel.Angular = float3.zero;
                        velocities[i] = vel;
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