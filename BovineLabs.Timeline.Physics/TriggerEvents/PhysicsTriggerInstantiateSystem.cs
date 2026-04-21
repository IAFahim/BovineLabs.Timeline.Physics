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
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
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

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
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
                CollisionEventsLookup = _collisionEventsLookup
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

        [BurstCompile]
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

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var configs = chunk.GetNativeArray(ref DataHandle);
                var trackBindings = chunk.GetNativeArray(ref TrackBindingHandle);

                using var spawnsPool = PooledNativeList<SpawnData>.Make();
                var spawns = spawnsPool.List;

                for (var i = 0; i < chunk.Count; i++)
                {
                    var self = trackBindings[i].Value;
                    var cfg = configs[i];

                    if (!Registry.TryGetValue(cfg.ObjectId, out var prefab) || prefab == Entity.Null)
                    {
                        Logger.LogError($"Prefab not found for ObjectId {cfg.ObjectId.ID}");
                        continue;
                    }

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(evt.EntityB)) continue;

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            spawns.AddNoResize(new SpawnData
                            {
                                Prefab = prefab,
                                Self = self,
                                Other = evt.EntityB,
                                Config = cfg,
                                ContactPoint = midpoint,
                                ContactNormal = dir
                            });
                        }

                    if (!CollisionEventsLookup.TryGetBuffer(self, out var collisions)) continue;
                    foreach (var evt in collisions)
                    {
                        if (evt.State != cfg.EventState) continue;

                        var selfPos = LocalToWorldLookup[self].Position;
                        var otherPos = LocalToWorldLookup[evt.EntityB].Position;

                        var hasContact = evt.TryGetDetails(out var details);
                        var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                        var normal = hasContact ? evt.Normal : math.normalizesafe(selfPos - otherPos);

                        spawns.AddNoResize(new SpawnData
                        {
                            Prefab = prefab,
                            Self = self,
                            Other = evt.EntityB,
                            Config = cfg,
                            ContactPoint = pt,
                            ContactNormal = normal
                        });
                    }
                }

                var spawnsArray = spawns.AsArray();
                for (var i = 0; i < spawnsArray.Length; i++) Spawn(unfilteredChunkIndex, in spawnsArray.ElementAtRO(i));
            }

            private void Spawn(int chunkIndex, in SpawnData spawn)
            {
                var selfLtw = LocalToWorldLookup[spawn.Self];
                var otherLtw = LocalToWorldLookup[spawn.Other];
                
                var targets = TargetsLookup.HasComponent(spawn.Self) ? TargetsLookup[spawn.Self] : default;

                float3 resolvedPosOffset = spawn.Config.PositionOffset;
                if (spawn.Config.PositionOffsetSpace != Target.None)
                {
                    var spaceEntity = PhysicsTriggerResolution.ResolveTarget(spawn.Config.PositionOffsetSpace, spawn.Self, spawn.Other, targets, TargetsCustomLookup);
                    if (spaceEntity != Entity.Null && LocalToWorldLookup.TryGetComponent(spaceEntity, out var spaceLtw))
                    {
                        resolvedPosOffset = math.rotate(spaceLtw.Rotation, spawn.Config.PositionOffset);
                    }
                }

                var transform = PhysicsTriggerResolution.CalculateTransform(
                    spawn.Config.PositionMode, resolvedPosOffset,
                    spawn.Config.RotationMode, spawn.Config.RotationOffsetEuler,
                    selfLtw, otherLtw, spawn.ContactPoint, spawn.ContactNormal);

                var commands = new CommandBufferParallelCommands(ECB, chunkIndex);
                commands.Instantiate(spawn.Prefab);
                commands.SetComponent(transform);

                commands.SetComponent(new Targets
                {
                    Owner = targets.Owner,
                    Source = targets.Source,
                    Target = spawn.Other
                });

                if (spawn.Config.AssignParent != Target.None)
                {
                    var parentEntity = PhysicsTriggerResolution.ResolveTarget(spawn.Config.AssignParent, spawn.Self,
                        spawn.Other, targets, TargetsCustomLookup);
                    
                    if (parentEntity != Entity.Null &&
                        LocalToWorldLookup.TryGetComponent(parentEntity, out var parentLtw))
                    {
                        commands.AddComponent(new Parent { Value = parentEntity });
                        var worldMatrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);
                        var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                        commands.SetComponent(LocalTransform.FromMatrix(localMatrix));
                    }
                }
            }
        }
    }
}