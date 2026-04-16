using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);

            state.Dependency = new InstantiateJob
            {
                ECB = ecb,
                Registry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct InstantiateJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ObjectDefinitionRegistry Registry;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;
                var prefab = Registry[cfg.ObjectId];
                if (prefab == Entity.Null) return;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    var i = 0;
                    while (i < triggers.Length)
                    {
                        var evt = triggers[i];
                        if (evt.State == cfg.EventState)
                        {
                            var midpoint = (LocalToWorldLookup[self].Position + LocalToWorldLookup[evt.EntityB].Position) * 0.5f;
                            var dir = math.normalizesafe(LocalToWorldLookup[self].Position - LocalToWorldLookup[evt.EntityB].Position);
                            Spawn(chunkIndex, self, evt.EntityB, prefab, cfg, midpoint, dir);
                        }
                        i++;
                    }
                }

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    var i = 0;
                    while (i < collisions.Length)
                    {
                        var evt = collisions[i];
                        if (evt.State == cfg.EventState)
                        {
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (LocalToWorldLookup[self].Position + LocalToWorldLookup[evt.EntityB].Position) * 0.5f;
                            var normal = hasContact ? evt.Normal : math.normalizesafe(LocalToWorldLookup[self].Position - LocalToWorldLookup[evt.EntityB].Position);
                            Spawn(chunkIndex, self, evt.EntityB, prefab, cfg, pt, normal);
                        }
                        i++;
                    }
                }
            }

            private void Spawn(int chunkIndex, Entity self, Entity other, Entity prefab, in PhysicsTriggerInstantiateData cfg, float3 contactPoint, float3 contactNormal)
            {
                if (!LocalToWorldLookup.TryGetComponent(self, out var selfLtw) || !LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

                var transform = PhysicsTriggerResolution.CalculateTransform(
                    cfg.PositionMode, cfg.PositionOffset, cfg.IsPositionOffsetLocal,
                    cfg.RotationMode, cfg.RotationOffsetEuler,
                    selfLtw, otherLtw, contactPoint, contactNormal);

                var instance = ECB.Instantiate(chunkIndex, prefab);
                ECB.SetComponent(chunkIndex, instance, transform);

                var selfTargets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;
                
                var newTargets = new Targets
                {
                    Owner = selfTargets.Owner != Entity.Null ? selfTargets.Owner : self,
                    Source = self,
                    Target = other
                };
                ECB.SetComponent(chunkIndex, instance, newTargets);

                if (cfg.AssignParent)
                {
                    var parentEntity = PhysicsTriggerResolution.ResolveTarget(cfg.ParentTarget, self, other, selfTargets);
                    if (parentEntity != Entity.Null && LocalToWorldLookup.TryGetComponent(parentEntity, out var parentLtw))
                    {
                        ECB.AddComponent(chunkIndex, instance, new Parent { Value = parentEntity });
                        var worldMatrix = float4x4.TRS(transform.Position, transform.Rotation, transform.Scale);
                        var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                        ECB.SetComponent(chunkIndex, instance, LocalTransform.FromMatrix(localMatrix));
                    }
                }
            }
        }
    }
}
