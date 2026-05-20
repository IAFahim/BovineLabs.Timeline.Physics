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
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using EntityCache = BovineLabs.Core.Extensions.EntityCache;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Child> _childLookup;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            state.RequireForUpdate<BLLogger>();

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _childLookup = state.GetBufferLookup<Child>(true);

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerInstantiateData, ClipActive>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _childLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var configType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerInstantiateData>(true);
            var activePrevType = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true);

            state.Dependency = new InstantiateGatherJob
            {
                ECB = ecb.AsParallelWriter(),
                Logger = SystemAPI.GetSingleton<BLLogger>(),
                Registry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                TrackBindingTypeHandle = bindingType,
                PhysicsTriggerInstantiateDataTypeHandle = configType,
                ClipActivePreviousTypeHandle = activePrevType,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ChildLookup = _childLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct InstantiateGatherJob : IJobChunk
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public BLLogger Logger;
            [ReadOnly] public ObjectDefinitionRegistry Registry;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerInstantiateData> PhysicsTriggerInstantiateDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Child> ChildLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref PhysicsTriggerInstantiateDataTypeHandle);

                var hasActivePrev = chunk.Has(ref ClipActivePreviousTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var binding = bindings[i];
                    var self = binding.Value;
                    if (self == Entity.Null) continue;

                    var cfg = configs[i];
                    var isFirstFrame = !hasActivePrev || !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);

                    if (!Registry.TryGetValue(cfg.ObjectId, out var prefab) || prefab == Entity.Null)
                    {
                        Logger.LogError($"Prefab not found for ObjectId {cfg.ObjectId.ID}");
                        continue;
                    }

                    var selfCache = EntityCache.Create(TargetsLookup, self);
                    var targets = TargetsLookup.TryGetComponent(ref selfCache, out var selfTargets)
                        ? selfTargets
                        : default;

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, cfg.EventState, isFirstFrame, false) || !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            Spawn(unfilteredChunkIndex, prefab, self, evt.EntityB, in cfg, midpoint, dir, in targets);
                        }

                    if (!CollisionEventsLookup.TryGetBuffer(self, out var collisions)) continue;
                    foreach (var evt in collisions)
                    {
                        if (!StatefulEventMatching.Matches(evt.State, cfg.EventState, isFirstFrame, false) || !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

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
                in PhysicsTriggerInstantiateData cfg, float3 contactPoint, float3 contactNormal, in Targets targets)
            {
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
                    Owner = targets.Owner,
                    Source = targets.Source,
                    Target = spawnTarget
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