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
    public partial struct PhysicsPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsPIDData, PhysicsPIDAnimated> _blendImpl;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            // 1. Prepare Authored data for blending
            state.Dependency = new PreparePIDDataJob().ScheduleParallel(state.Dependency);

            // 2. Thread-safe blend resolution
            var blendData = _blendImpl.Update(ref state);

            // 3. Apply physics math
            state.Dependency = new ApplyPIDVelocityJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(false),
                PIDStateLookup = SystemAPI.GetComponentLookup<PhysicsPIDState>(false)
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PreparePIDDataJob : IJobEntity
        {
            private void Execute(ref PhysicsPIDAnimated animated)
            {
                animated.Value = animated.AuthoredData;
            }
        }

        [BurstCompile]
        private struct ApplyPIDVelocityJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsPIDData>>.ReadOnly BlendData;
            [ReadOnly] public float DeltaTime;
            
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsPIDState> PIDStateLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!LocalTransformLookup.TryGetComponent(entity, out var transform)) return;
                if (!PIDStateLookup.TryGetComponent(entity, out var pidState)) return;

                var blendedPID = JobHelpers.Blend<PhysicsPIDData, PhysicsPIDMixer>(ref mixData, default);

                // Target is simply X units away from our current rotation (The "Carrot")
                var targetPosition = transform.Position + math.rotate(transform.Rotation, blendedPID.LocalTargetOffset);
                var error = targetPosition - transform.Position;

                if (!pidState.IsInitialized)
                {
                    pidState.PreviousError = error;
                    pidState.IntegralAccumulator = float3.zero;
                    pidState.IsInitialized = true;
                }

                // PID Math
                pidState.IntegralAccumulator += error * DeltaTime;
                var derivative = (error - pidState.PreviousError) / DeltaTime;
                pidState.PreviousError = error;

                var force = (blendedPID.Proportional * error) 
                          + (blendedPID.Integral * pidState.IntegralAccumulator) 
                          + (blendedPID.Derivative * derivative);

                // Clamp Force to Max
                var forceMagSq = math.lengthsq(force);
                if (forceMagSq > (blendedPID.MaxForce * blendedPID.MaxForce))
                {
                    force = math.normalize(force) * blendedPID.MaxForce;
                }

                // Apply to Linear Velocity
                velocity.Linear += force * DeltaTime;

                PhysicsVelocityLookup[entity] = velocity;
                PIDStateLookup[entity] = pidState;
            }
        }
    }
}