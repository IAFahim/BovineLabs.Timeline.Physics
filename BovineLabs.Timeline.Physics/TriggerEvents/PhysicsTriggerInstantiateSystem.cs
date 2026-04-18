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
        private EntityQuery query;
        private EntityTypeHandle entityHandle;
        private ComponentTypeHandle<PhysicsTriggerInstantiateData> dataHandle;
        
        private ComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private BufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private BufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            state.RequireForUpdate<BLLogger>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<InstantiateGatherJob>();

            query = SystemAPI.QueryBuilder()
                .WithAll<ClipActive, PhysicsTriggerInstantiateData>()
                .Build();

            entityHandle = state.GetEntityTypeHandle();
            dataHandle = state.GetComponentTypeHandle<PhysicsTriggerInstantiateData>(true);
            
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            entityHandle.Update(ref state);
            dataHandle.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new InstantiateGatherJob
            {
                ECB = ecb.AsParallelWriter(),
                Logger = SystemAPI.GetSingleton<BLLogger>(),
                Registry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                EntityHandle = entityHandle,
                DataHandle = dataHandle,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
            }.ScheduleParallel(query, state.Dependency);
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
            
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerInstantiateData> DataHandle;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var configs = chunk.GetNativeArray(ref DataHandle);

                using var spawnsPool = PooledNativeList<SpawnData>.Make();
                var spawns = spawnsPool.List;

                for (var i = 0; i < chunk.Count; i++)
                {
                    var self = entities[i];
                    var cfg = configs[i];

                    if (!Registry.TryGetValue(cfg.ObjectId, out var prefab) || prefab == Entity.Null)
                    {
                        Logger.LogError($"Prefab not found for ObjectId {cfg.ObjectId.ID}");
                        continue;
                    }

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    {
                        for (var j = 0; j < triggers.Length; j++)
                        {
                            var evt = triggers[j];
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(self) || !LocalToWorldLookup.HasComponent(evt.EntityB))
                            {
                                continue;
                            }

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
                    }

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                    {
                        for (var j = 0; j < collisions.Length; j++)
                        {
                            var evt = collisions[j];
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(self) || !LocalToWorldLookup.HasComponent(evt.EntityB))
                            {
                                continue;
                            }

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
                }

                var spawnsArray = spawns.AsArray();
                for (var i = 0; i < spawnsArray.Length; i++)
                {
                    Spawn(unfilteredChunkIndex, in spawnsArray.ElementAtRO(i));
                }
            }

            private void Spawn(int chunkIndex, in SpawnData spawn)
            {
                var selfLtw = LocalToWorldLookup[spawn.Self];
                var otherLtw = LocalToWorldLookup[spawn.Other];

                var transform = PhysicsTriggerResolution.CalculateTransform(
                    spawn.Config.PositionMode, spawn.Config.PositionOffset, spawn.Config.IsPositionOffsetLocal,
                    spawn.Config.RotationMode, spawn.Config.RotationOffsetEuler,
                    selfLtw, otherLtw, spawn.ContactPoint, spawn.ContactNormal);

                var commands = new CommandBufferParallelCommands(ECB, chunkIndex);
                var instance = commands.Instantiate(spawn.Prefab);
                
                commands.SetComponent(transform);

                var selfTargets = TargetsLookup.TryGetComponent(spawn.Self, out var t) ? t : default;
                
                commands.SetComponent(new Targets
                {
                    Owner = selfTargets.Owner != Entity.Null ? selfTargets.Owner : spawn.Self,
                    Source = spawn.Self,
                    Target = spawn.Other
                });

                if (spawn.Config.AssignParent)
                {
                    var parentEntity = PhysicsTriggerResolution.ResolveTarget(spawn.Config.ParentTarget, spawn.Self, spawn.Other, selfTargets);
                    if (parentEntity != Entity.Null && LocalToWorldLookup.TryGetComponent(parentEntity, out var parentLtw))
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