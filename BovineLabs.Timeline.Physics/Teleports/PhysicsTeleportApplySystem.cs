using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Stats;
using BovineLabs.Timeline.Physics.TriggerEvents;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Teleports
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
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
        private ConditionEventWriter.SingletonData _conditionWritersSingletonData;

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
            _conditionWritersSingletonData.Create(ref state);
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
            _conditionWriters.Update(ref state, _conditionWritersSingletonData);

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new TeleportJob
            {
                ECB = ecb,
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

            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
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
                    if (teleportStates[i].Fired) continue;

                    var selfEntity = entities[i];
                    var config = actives[i].Config;

                    teleportStates[i] = new PhysicsTeleportState { Fired = true };

                    var frame = TeleportResolver.Resolve(
                        selfEntity, config, float3.zero,
                        LocalToWorldLookup, TargetsLookup, LinkSources, Links);

                    if (!frame.HasTeleportedEntity) continue;

                    var targets = TargetsLookup.TryGetComponent(selfEntity, out var t) ? t : default;

                    if (config.RequireLineOfSight && !TeleportMath.CheckLineOfSight(
                            in CollisionWorld, frame.TeleportedPosition, frame.LandingPosition,
                            config.LineOfSightOffset, config.ObstacleMask, frame.TeleportedEntity, frame.LandingEntity))
                    {
                        FireFailure(selfEntity, frame.TeleportedEntity, config, targets);
                        continue;
                    }

                    var radiusMultiplier = StatStrengthUtility.Resolve(
                        in config.Strength, selfEntity, targets, LinkSources, Links, StatLookup);
                    var radius = config.Radius * math.max(radiusMultiplier, 0f);

                    if (!TeleportSearch.TryFindLanding(in frame, in config, radius, in CollisionWorld, out var landing))
                    {
                        FireFailure(selfEntity, frame.TeleportedEntity, config, targets);
                        continue;
                    }

                    var localScale = LocalTransformLookup.TryGetComponent(frame.TeleportedEntity, out var current)
                        ? current.Scale
                        : 1f;
                    var transform = TeleportPlacement.ComputeLocalTransform(
                        in frame, in config, landing, localScale, LocalToWorldLookup, ParentLookup);

                    Commit(unfilteredChunkIndex, frame.TeleportedEntity, transform, config.ResetVelocity);
                }
            }

            private void Commit(int sortKey, Entity target, LocalTransform transform, bool resetVelocity)
            {
                if (LocalTransformLookup.HasComponent(target))
                    ECB.SetComponent(sortKey, target, transform);

                if (resetVelocity && PhysicsVelocityLookup.HasComponent(target))
                {
                    var velocity = PhysicsVelocityLookup[target];
                    velocity.Linear = float3.zero;
                    velocity.Angular = float3.zero;
                    ECB.SetComponent(sortKey, target, velocity);
                }
            }

            private void FireFailure(Entity selfEntity, Entity targetEntity, in PhysicsTeleportData config,
                in Targets targets)
            {
                if (config.FailureCondition == ConditionKey.Null) return;

                if (!PhysicsTriggerResolution.TryResolveLinkedTarget(
                        config.FailureRouteTo.ReadRootFrom, config.FailureRouteTo.LinkKey,
                        selfEntity, targetEntity, targets, LinkSources, Links,
                        out var routeTarget))
                    return;

                if (ConditionWriters.TryGet(routeTarget, out var writer))
                    writer.Trigger(config.FailureCondition, config.FailureValue);
            }
        }
    }
}