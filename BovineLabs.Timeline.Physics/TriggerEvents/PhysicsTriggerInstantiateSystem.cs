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
        private UnsafeComponentLookup<LocalToWorld> localToWorldLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            localToWorldLookup.Update(ref state);
            targetsLookup.Update(ref state);
            triggerEventsLookup.Update(ref state);
            collisionEventsLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecb,
                ObjectDefinitionRegistry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                LocalToWorldLookup = localToWorldLookup,
                TargetsLookup = targetsLookup,
                TriggerEventsLookup = triggerEventsLookup,
                CollisionEventsLookup = collisionEventsLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            [WriteOnly] public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ObjectDefinitionRegistry ObjectDefinitionRegistry;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    foreach (var evt in triggers)
                    {
                        if (evt.State == cfg.EventState)
                        {
                            TryProcessSpawn(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);
                        }
                    }
                }

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    foreach (var evt in collisions)
                    {
                        if (evt.State == cfg.EventState)
                        {
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : float3.zero;
                            TryProcessSpawn(chunkIndex, self, evt.EntityB, cfg, pt, evt.Normal, hasContact);
                        }
                    }
                }
            }

            private bool TryProcessSpawn(int chunkIndex, Entity self, Entity other, in PhysicsTriggerInstantiateData cfg, in float3 contactPoint, in float3 contactNormal, bool hasContactData)
            {
                if (!LocalToWorldLookup.TryGetComponent(self, out var selfLtw)) return false;
                if (!LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return false;
                if (!TriggerResolution.TryResolveLocalTransform(cfg, selfLtw, otherLtw, contactPoint, contactNormal, hasContactData, out var localTransform)) return false;

                var instance = ECB.Instantiate(chunkIndex, ObjectDefinitionRegistry[cfg.ObjectId]);
                ECB.SetComponent(chunkIndex, instance, localTransform);

                if (TriggerResolution.TryResolveParent(cfg.ParentMode, self, other, TargetsLookup, out var parentEntity))
                {
                    ECB.AddComponent(chunkIndex, instance, new Parent { Value = parentEntity });
                    if (TriggerResolution.TryResolveRelativeTransform(localTransform, parentEntity, LocalToWorldLookup, out var relativeTransform))
                    {
                        ECB.SetComponent(chunkIndex, instance, relativeTransform);
                    }
                }

                if (TriggerResolution.TryResolveTargets(self, other, TargetsLookup, out var inheritedTargets))
                {
                    ECB.SetComponent(chunkIndex, instance, inheritedTargets);
                }

                return true;
            }
        }
    }
}