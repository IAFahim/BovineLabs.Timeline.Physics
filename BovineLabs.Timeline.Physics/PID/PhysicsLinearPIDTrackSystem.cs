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
    public partial struct PhysicsLinearPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsLinearPIDData, PhysicsLinearPIDAnimated> blendImpl;
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private ComponentLookup<TargetsCustom> targetsCustomLookup;
        private UnsafeComponentLookup<PhysicsVelocity> physicsVelocityLookup;
        private UnsafeComponentLookup<PhysicsMass> physicsMassLookup;
        private UnsafeComponentLookup<PhysicsLinearPIDState> pidStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();
            physicsMassLookup = state.GetUnsafeComponentLookup<PhysicsMass>(true);
            pidStateLookup = state.GetUnsafeComponentLookup<PhysicsLinearPIDState>();
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
            targetsCustomLookup.Update(ref state);
            physicsVelocityLookup.Update(ref state);
            physicsMassLookup.Update(ref state);
            pidStateLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            var blendData = blendImpl.Update(ref state);

            state.Dependency = new ApplyJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = localTransformLookup,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomLookup,
                PhysicsVelocityLookup = physicsVelocityLookup,
                PhysicsMassLookup = physicsMassLookup,
                PIDStateLookup = pidStateLookup
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsLinearPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        private struct ApplyJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsLinearPIDData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsMass> PhysicsMassLookup;
            public UnsafeComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public UnsafeComponentLookup<PhysicsLinearPIDState> PIDStateLookup;
            public float DeltaTime;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!PhysicsMassLookup.TryGetComponent(entity, out var mass)) return;
                if (!LocalTransformLookup.TryGetComponent(entity, out var transform)) return;
                if (!PIDStateLookup.TryGetComponent(entity, out var pidState)) return;

                var blended = JobHelpers.Blend<PhysicsLinearPIDData, PhysicsLinearPIDMixer>(ref mixData, default);

                if (PhysicsMath.TryResolveLinearPidTarget(transform, blended, entity, TargetsLookup, TargetsCustomLookup, LocalTransformLookup, out var targetPosition) &&
                    PhysicsMath.TryCalculateLinearPidForce(transform.Position, targetPosition, blended, pidState, DeltaTime, out var force, out var nextIntegral, out var nextPrevError) &&
                    PhysicsMath.TryApplyLinearForce(velocity, mass, force, DeltaTime, out var nextVelocity))
                {
                    PhysicsVelocityLookup[entity] = nextVelocity;
                    PIDStateLookup[entity] = new PhysicsLinearPIDState
                    {
                        IntegralAccumulator = nextIntegral,
                        PreviousError = nextPrevError,
                        IsInitialized = true
                    };
                }
            }
        }
    }
}