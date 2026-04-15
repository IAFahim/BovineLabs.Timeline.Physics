using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsPIDData, PhysicsPIDAnimated> blendImpl;
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<PhysicsVelocity> physicsVelocityLookup;
        private UnsafeComponentLookup<PhysicsMass> physicsMassLookup;
        private UnsafeComponentLookup<PhysicsPIDState> pidStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();
            physicsMassLookup = state.GetUnsafeComponentLookup<PhysicsMass>(true);
            pidStateLookup = state.GetUnsafeComponentLookup<PhysicsPIDState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            localTransformLookup.Update(ref state);
            targetsLookup.Update(ref state);
            physicsVelocityLookup.Update(ref state);
            physicsMassLookup.Update(ref state);
            pidStateLookup.Update(ref state);

            state.Dependency = new PreparePIDDataJob().ScheduleParallel(state.Dependency);
            var blendData = blendImpl.Update(ref state);

            state.Dependency = new ApplyPIDForceJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = localTransformLookup,
                TargetsLookup = targetsLookup,
                PhysicsVelocityLookup = physicsVelocityLookup,
                PhysicsMassLookup = physicsMassLookup,
                PIDStateLookup = pidStateLookup
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PreparePIDDataJob : IJobEntity
        {
            private void Execute(ref PhysicsPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        private struct ApplyPIDForceJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsPIDData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsMass> PhysicsMassLookup;
            public UnsafeComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public UnsafeComponentLookup<PhysicsPIDState> PIDStateLookup;
            public float DeltaTime;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!PhysicsMassLookup.TryGetComponent(entity, out var mass)) return;
                if (!LocalTransformLookup.TryGetComponent(entity, out var transform)) return;
                if (!PIDStateLookup.TryGetComponent(entity, out var pidState)) return;

                var blended = JobHelpers.Blend<PhysicsPIDData, PhysicsPIDMixer>(ref mixData, default);

                if (PhysicsMath.TryResolvePIDTarget(transform, blended, entity, TargetsLookup, LocalTransformLookup, out var targetPosition) &&
                    PhysicsMath.TryCalculatePIDForce(transform.Position, targetPosition, blended, pidState, DeltaTime, out var force, out var nextState))
                {
                    velocity.Linear += force * mass.InverseMass * DeltaTime;
                    
                    PhysicsVelocityLookup[entity] = velocity;
                    PIDStateLookup[entity] = nextState;
                }
            }
        }
    }
}