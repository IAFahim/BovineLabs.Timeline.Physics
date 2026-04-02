using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Instantiate;
using BovineLabs.Timeline.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct StatefulCollisionInstantiateSystem : ISystem
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
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);
            var targetsLookup = SystemAPI.GetComponentLookup<Targets>(true);
            var triggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true);

            var job = new PhysicsInstantiateJob
            {
                ECB = ecb,
                LocalToWorldLookup = localToWorldLookup,
                TargetsLookup = targetsLookup,
                TriggerEventsLookup = triggerEventsLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive), typeof(OnClipActiveStatefulInstantiateTag))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            [WriteOnly] public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;

            private void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                in TrackBinding binding,
                in StatefulEventStateConfig statefulEventStateConfig,
                in InstantiateConfigComponent instantiateConfigComponent
            )
            {
                var statefulSelf = binding.Value;
                if (!TriggerEventsLookup.TryGetBuffer(statefulSelf, out var statefulTriggerEvents)) return;

                foreach (var statefulTriggerEvent in statefulTriggerEvents)
                {
                    if ((int)statefulTriggerEvent.State != (int)statefulEventStateConfig.Value) continue;
                    var otherEntity = statefulTriggerEvent.EntityB;
                    var otherHasTarget = TargetsLookup.HasComponent(otherEntity);
                    if (!otherHasTarget) continue;

                    var instance = ECB.Instantiate(chunkIndex, instantiateConfigComponent.Prefab);

                    var trackBindingTargets = TargetsLookup[statefulSelf];
                    var otherBindingTargets = TargetsLookup[otherEntity];
                    ECB.SetComponent(chunkIndex, instance, new Targets
                    {
                        Owner = trackBindingTargets.Owner,
                        Source = trackBindingTargets.Source,
                        Target = otherBindingTargets.Source
                    });
                    if ((instantiateConfigComponent.ParentTransformConfig & ParentTransformConfig.SetTransform) != 0)
                    {
                        LocalToWorldLookup[statefulSelf].Value.ExtractLocalTransform(out var localTransform);
                        ECB.SetComponent(chunkIndex, instance, localTransform);
                    }
                }
            }
        }
    }
}
