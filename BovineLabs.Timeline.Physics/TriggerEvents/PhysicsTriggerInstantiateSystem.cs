using System;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.EntityCommands;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Core.Utility;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    using EntityCache = EntityCache;

    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<TrackBinding> _trackBindingHandle;
        private ComponentTypeHandle<PhysicsTriggerInstantiateData> _dataHandle;
        private ComponentTypeHandle<PhysicsTriggerFilterData> _filterHandle;
        private ComponentTypeHandle<ClipActivePrevious> _activePrevHandle;
        private ComponentTypeHandle<TimerData> _timerDataHandle;
        private ComponentTypeHandle<TimeTransform> _timeTransformHandle;

        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Child> _childLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            state.RequireForUpdate<BLLogger>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<InstantiateGatherJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, ClipActive, PhysicsTriggerInstantiateData, PhysicsTriggerFilterData>()
                .Build();

            _trackBindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);
            _dataHandle = state.GetComponentTypeHandle<PhysicsTriggerInstantiateData>(true);
            _filterHandle = state.GetComponentTypeHandle<PhysicsTriggerFilterData>(true);
            _activePrevHandle = state.GetComponentTypeHandle<ClipActivePrevious>(true);
            _timerDataHandle = state.GetComponentTypeHandle<TimerData>(true);
            _timeTransformHandle = state.GetComponentTypeHandle<TimeTransform>(true);

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _childLookup = state.GetBufferLookup<Child>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _trackBindingHandle.Update(ref state);
            _dataHandle.Update(ref state);
            _filterHandle.Update(ref state);
            _activePrevHandle.Update(ref state);
            _timerDataHandle.Update(ref state);
            _timeTransformHandle.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _childLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var spawned = new NativeParallelHashSet<SpawnKey>(64, state.WorldUpdateAllocator);

            state.Dependency = new InstantiateGatherJob
            {
                ECB = ecb.AsParallelWriter(),
                Spawned = spawned.AsParallelWriter(),
                Logger = SystemAPI.GetSingleton<BLLogger>(),
                Registry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                TrackBindingHandle = _trackBindingHandle,
                DataHandle = _dataHandle,
                FilterHandle = _filterHandle,
                ClipActivePreviousTypeHandle = _activePrevHandle,
                TimerDataTypeHandle = _timerDataHandle,
                TimeTransformTypeHandle = _timeTransformHandle,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ChildLookup = _childLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        private struct SpawnData
        {
            public Entity Prefab;
            public Entity Self;
            public Entity Other;
            public PhysicsTriggerInstantiateData Config;
            public float3 ContactPoint;
            public float3 ContactNormal;
        }

        private struct SpawnKey : IEquatable<SpawnKey>
        {
            public Entity Self;
            public Entity Other;
            public ObjectId ObjectId;

            public bool Equals(SpawnKey other)
            {
                return Self == other.Self && Other == other.Other && ObjectId.Equals(other.ObjectId);
            }

            public override int GetHashCode()
            {
                return (int)math.hash(new int3(Self.Index, Other.Index, ObjectId.GetHashCode()));
            }
        }

        private struct InstantiateGatherJob : IJobChunkWorkerBeginEnd
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public NativeParallelHashSet<SpawnKey>.ParallelWriter Spawned;
            public BLLogger Logger;
            [ReadOnly] public ObjectDefinitionRegistry Registry;

            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerInstantiateData> DataHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> FilterHandle;
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimerData> TimerDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimeTransform> TimeTransformTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Child> ChildLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var configs = chunk.GetNativeArray(ref DataHandle);
                var filters = chunk.GetNativeArray(ref FilterHandle);
                var trackBindings = chunk.GetNativeArray(ref TrackBindingHandle);

                var hasActivePrev = chunk.Has(ref ClipActivePreviousTypeHandle);
                var hasTiming = chunk.Has(ref TimerDataTypeHandle) && chunk.Has(ref TimeTransformTypeHandle);
                var timers = hasTiming ? chunk.GetNativeArray(ref TimerDataTypeHandle) : default;
                var timeTransforms = hasTiming ? chunk.GetNativeArray(ref TimeTransformTypeHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var self = trackBindings[i].Value;
                    if (self == Entity.Null || !LocalToWorldLookup.HasComponent(self)) continue;

                    var cfg = configs[i];
                    var filter = filters[i];

                    var isFirstFrame = !hasActivePrev || !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);
                    var isLastFrame = false;
                    if (hasTiming)
                    {
                        var timer = timers[i];
                        var timeTransform = timeTransforms[i];
                        isLastFrame = StatefulEventMatching.IsClipLastFrame(in timer, in timeTransform);
                    }

                    if (!Registry.TryGetValue(cfg.ObjectId, out var prefab) || prefab == Entity.Null)
                    {
                        Logger.LogError($"Prefab not found for ObjectId {cfg.ObjectId.ID}");
                        continue;
                    }

                    var targets = TargetsLookup.TryGetComponent(self, out var selfTargets)
                        ? selfTargets
                        : default;

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, cfg.EventState, isFirstFrame, isLastFrame) ||
                                !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

                            if (!PhysicsTriggerFiltering.IsValidTarget(self, evt.EntityB, in filter, in targets,
                                    LinkSources, Links)) continue;

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            Spawn(unfilteredChunkIndex, prefab, self, evt.EntityB, in cfg, midpoint, dir, in targets);
                        }

                    if (!CollisionEventsLookup.TryGetBuffer(self, out var collisions)) continue;
                    foreach (var evt in collisions)
                    {
                        if (!StatefulEventMatching.Matches(evt.State, cfg.EventState, isFirstFrame, isLastFrame) ||
                            !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

                        if (!PhysicsTriggerFiltering.IsValidTarget(self, evt.EntityB, in filter, in targets,
                                LinkSources, Links)) continue;

                        var selfPos = LocalToWorldLookup[self].Position;
                        var otherPos = LocalToWorldLookup[evt.EntityB].Position;

                        var hasContact = evt.TryGetDetails(out var details);
                        var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                        var normal = hasContact ? evt.Normal : math.normalizesafe(selfPos - otherPos);

                        Spawn(unfilteredChunkIndex, prefab, self, evt.EntityB, in cfg, pt, normal, in targets);
                    }
                }
            }

            private void Spawn(int chunkIndex, Entity prefab, Entity self, Entity other,
                in PhysicsTriggerInstantiateData cfg, float3 contactPoint, float3 contactNormal, in Targets targets
            )
            {
                if (!Spawned.Add(new SpawnKey { Self = self, Other = other, ObjectId = cfg.ObjectId }))
                    return;

                var spawnTarget = other;
                if (cfg.TargetLinkKey != 0)
                    if (PhysicsTriggerResolution.TryResolveLinkedTarget(
                            Target.Target, cfg.TargetLinkKey, self, other, targets, LinkSources,
                            Links, out var resolvedTarget))
                        spawnTarget = resolvedTarget;

                var parent = Entity.Null;
                if (cfg.AssignParent != Target.None)
                    PhysicsTriggerResolution.TryResolveLinkedTarget(
                        cfg.AssignParent, cfg.AssignParentLinkKey, self, other, targets,
                        LinkSources, Links, out parent);

                var selfLtw = LocalToWorldLookup[self];
                var targetLtw = LocalToWorldLookup.HasComponent(spawnTarget)
                    ? LocalToWorldLookup[spawnTarget]
                    : LocalToWorldLookup[other];

                var resolvedPosOffset = cfg.PositionOffset;
                if (cfg.PositionOffsetSpace != Target.None)
                    if (PhysicsTriggerResolution.TryResolveTarget(cfg.PositionOffsetSpace, self, other, targets,
                            out var spaceEntity)
                        && LocalToWorldLookup.TryGetComponent(spaceEntity, out var spaceLtw))
                        resolvedPosOffset = math.rotate(spaceLtw.Rotation, cfg.PositionOffset);

                PhysicsTriggerResolution.TryCalculateTransform(
                    cfg.PositionMode, resolvedPosOffset,
                    cfg.RotationMode, cfg.RotationOffsetEuler,
                    selfLtw, targetLtw, contactPoint, contactNormal,
                    out var transform);

                var instance = ECB.Instantiate(chunkIndex, prefab);
                var commands = new CommandBufferParallelCommands(ECB, chunkIndex, instance);

                ECB.SetComponent(chunkIndex, instance, new Targets
                {
                    Owner = targets.Owner == Entity.Null ? self : targets.Owner,
                    Source = targets.Source == Entity.Null ? self : targets.Source,
                    Target = spawnTarget,
                    Custom = targets.Custom
                });

                if (parent != Entity.Null && LocalToWorldLookup.TryGetComponent(parent, out var parentLtw))
                {
                    var worldMatrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);
                    var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                    transform = LocalTransform.FromMatrix(localMatrix);

                    var parentCache = EntityCache.Create(ChildLookup, parent);
                    var childs = ChildLookup.TryGetBuffer(ref parentCache, out var parentChilds)
                        ? parentChilds
                        : default;
                    TransformUtility.SetupParent(ref commands, parent, instance, parentLtw, transform, childs);
                }

                ECB.SetComponent(chunkIndex, instance, transform);
            }
        }
    }
}
