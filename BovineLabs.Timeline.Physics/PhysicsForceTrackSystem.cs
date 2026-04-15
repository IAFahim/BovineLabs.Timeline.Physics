using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsForceTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsForceData, PhysicsForceAnimated> blendImpl;
        private UnsafeComponentLookup<PhysicsVelocity> physicsVelocityLookup;
        private UnsafeComponentLookup<PhysicsMass> physicsMassLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();
            physicsMassLookup = state.GetUnsafeComponentLookup<PhysicsMass>(true);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            physicsVelocityLookup.Update(ref state);
            physicsMassLookup.Update(ref state);

            state.Dependency = new PrepareForceDataJob().ScheduleParallel(state.Dependency);
            var blendData = blendImpl.Update(ref state);

            state.Dependency = new ApplyForceJob
            {
                BlendData = blendData,
                PhysicsVelocityLookup = physicsVelocityLookup,
                PhysicsMassLookup = physicsMassLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareForceDataJob : IJobEntity
        {
            private void Execute(ref PhysicsForceAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        private struct ApplyForceJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsForceData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<PhysicsMass> PhysicsMassLookup;
            public UnsafeComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public float DeltaTime;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!PhysicsMassLookup.TryGetComponent(entity, out var mass)) return;

                var blended = JobHelpers.Blend<PhysicsForceData, PhysicsForceMixer>(ref mixData, default);

                velocity.Linear += blended.Linear * mass.InverseMass * DeltaTime;
                velocity.Angular += math.mul(mass.InverseInertia, blended.Angular * DeltaTime);

                PhysicsVelocityLookup[entity] = velocity;
            }
        }
    }
}