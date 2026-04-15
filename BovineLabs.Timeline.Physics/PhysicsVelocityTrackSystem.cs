using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsVelocityTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsVelocityData, PhysicsVelocityAnimated> _blendImpl;
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> physicsVelocityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            localTransformLookup.Update(ref state);
            physicsVelocityLookup.Update(ref state);

            state.Dependency = new PrepareWorldVelocityJob
            {
                LocalTransformLookup = localTransformLookup
            }.ScheduleParallel(state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new ApplyVelocityJob
            {
                BlendData = blendData,
                PhysicsVelocityLookup = physicsVelocityLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareWorldVelocityJob : IJobEntity
        {
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;

            private void Execute(ref PhysicsVelocityAnimated animated, in TrackBinding binding)
            {
                var linear = animated.AuthoredVelocity.Linear;
                var angular = animated.AuthoredVelocity.Angular;

                if (animated.IsLocalSpace && LocalTransformLookup.TryGetComponent(binding.Value, out var transform))
                {
                    linear = math.rotate(transform.Rotation, linear);
                    angular = math.rotate(transform.Rotation, angular);
                }

                animated.Value = new PhysicsVelocityData
                {
                    Linear = linear,
                    Angular = angular
                };
            }
        }

        [BurstCompile]
        private struct ApplyVelocityJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsVelocityData>>.ReadOnly BlendData;
            public UnsafeComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public float DeltaTime;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;

                var blendedVelocity = JobHelpers.Blend<PhysicsVelocityData, PhysicsVelocityMixer>(ref mixData, default);

                velocity.Linear += blendedVelocity.Linear * DeltaTime;
                velocity.Angular += blendedVelocity.Angular * DeltaTime;

                PhysicsVelocityLookup[entity] = velocity;
            }
        }
    }
}