using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;

            private void Execute(
                [ChunkIndexInQuery] int chunkIndex, 
                in TrackBinding binding, 
                in PhysicsTriggerInstantiateData instantiateData)
            {
                var statefulSelf = binding.Value;
                if (!TriggerEventsLookup.TryGetBuffer(statefulSelf, out var statefulTriggerEvents)) return;

                foreach (var triggerEvent in statefulTriggerEvents)
                {
                    if (triggerEvent.State != instantiateData.EventState) continue;

                    var otherEntity = triggerEvent.EntityB;
                    if (!TargetsLookup.HasComponent(otherEntity)) continue;

                    // 1. Spawn independent prefab
                    var instance = ECB.Instantiate(chunkIndex, instantiateData.Prefab);

                    // 2. Forward target tracking data
                    var trackBindingTargets = TargetsLookup[statefulSelf];
                    var otherBindingTargets = TargetsLookup[otherEntity];
                    
                    ECB.SetComponent(chunkIndex, instance, new Targets
                    {
                        Owner = trackBindingTargets.Owner,
                        Source = trackBindingTargets.Source,
                        Target = otherBindingTargets.Source
                    });

                    // 3. Snap to transform seamlessly
                    if (instantiateData.SnapToTransform && LocalToWorldLookup.TryGetComponent(statefulSelf, out var ltw))
                    {
                        ltw.Value.ExtractLocalTransform(out var localTransform);
                        ECB.SetComponent(chunkIndex, instance, localTransform);
                    }
                }
            }
        }
    }
}