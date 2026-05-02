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

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<TrackBinding> _trackBindingHandle;
        private ComponentTypeHandle<PhysicsTriggerInstantiateData> _dataHandle;

        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;
        private ComponentLookup<EntityLinkSource> _linkSourceLookup;
        private BufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Child> _childLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            state.RequireForUpdate<BLLogger>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<InstantiateGatherJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<ClipActive, PhysicsTriggerInstantiateData>()
                .Build();

            _trackBindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);
            _dataHandle = state.GetComponentTypeHandle<PhysicsTriggerInstantiateData>(true);

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
            _linkSourceLookup = state.GetComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetBufferLookup<EntityLinkEntry>(true);
            _childLookup = state.GetBufferLookup<Child>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _trackBindingHandle.Update(ref state);
            _dataHandle.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _targetsCustomLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _childLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new InstantiateGatherJob
            {
                ECB = ecb.AsParallelWriter(),
                Logger = SystemAPI.GetSingleton<BLLogger>(),
                Registry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                TrackBindingHandle = _trackBindingHandle,
                DataHandle = _dataHandle,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
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

        private struct InstantiateGatherJob : IJobChunkWorkerBeginEnd
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            public BLLogger Logger;
            [ReadOnly] public ObjectDefinitionRegistry Registry;

            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerInstantiateData> DataHandle;
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingHandle;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;
            [ReadOnly] public ComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public BufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Child> ChildLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var configs = chunk.GetNativeArray(ref DataHandle);
                var trackBindings = chunk.GetNativeArray(ref TrackBindingHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var self = trackBindings[i].Value;
                    var cfg = configs[i];

                    if (!Registry.TryGetValue(cfg.ObjectId, out var prefab) || prefab == Entity.Null)
                    {
                        Logger.LogError($"Prefab not found for ObjectId {cfg.ObjectId.ID}");
                        continue;
                    }

                    var targets = TargetsLookup.HasComponent(self) ? TargetsLookup[self] : default;

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            Spawn(unfilteredChunkIndex, prefab, self, evt.EntityB, in cfg, midpoint, dir, in targets);
                        }

                    if (!CollisionEventsLookup.TryGetBuffer(self, out var collisions)) continue;
                    foreach (var evt in collisions)
                    {
                        if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

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
                {
                    if (PhysicsTriggerResolution.TryResolveLinkedTarget(
                            Target.Target, cfg.TargetLinkKey, self, other, targets, TargetsCustomLookup, LinkSources,
                            Links, out var resolvedTarget))
                    {
                        spawnTarget = resolvedTarget;
                    }
                }

                Entity parent = Entity.Null;
                if (cfg.AssignParent != Target.None)
                {
                    PhysicsTriggerResolution.TryResolveLinkedTarget(
                        cfg.AssignParent, cfg.AssignParentLinkKey, self, other, targets, TargetsCustomLookup,
                        LinkSources, Links, out parent);
                }

                var selfLtw = LocalToWorldLookup[self];
                var targetLtw = LocalToWorldLookup.HasComponent(spawnTarget)
                    ? LocalToWorldLookup[spawnTarget]
                    : LocalToWorldLookup[other];

                var resolvedPosOffset = cfg.PositionOffset;
                if (cfg.PositionOffsetSpace != Target.None)
                {
                    if (PhysicsTriggerResolution.TryResolveTarget(cfg.PositionOffsetSpace, self, other, targets,
                            TargetsCustomLookup, out var spaceEntity)
                        && LocalToWorldLookup.TryGetComponent(spaceEntity, out var spaceLtw))
                    {
                        resolvedPosOffset = math.rotate(spaceLtw.Rotation, cfg.PositionOffset);
                    }
                }

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

                    var childs = ChildLookup.HasBuffer(parent) ? ChildLookup[parent] : default;
                    TransformUtility.SetupParent(ref commands, parent, instance, parentLtw, transform, childs);
                }

                ECB.SetComponent(chunkIndex, instance, transform);
            }
        }
    }
}