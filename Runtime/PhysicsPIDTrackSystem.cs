using BovineLabs.Core.Jobs;
using BovineLabs.Reaction.Data.Core;
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
            if (dt <= 0f) return; // Prevent divide by zero in Derivative math

            // 1. Copy authored data to the Value property so the Blend system can read it
            state.Dependency = new PreparePIDDataJob().ScheduleParallel(state.Dependency);

            // 2. Blend overlapping timeline clips automatically and thread-safely
            var blendData = _blendImpl.Update(ref state);

            // 3. Apply the final PID math to the physics bodies
            state.Dependency = new ApplyPIDVelocityJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
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
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsPIDState> PIDStateLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var missileEntity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(missileEntity, out var velocity)) return;
                if (!LocalTransformLookup.TryGetComponent(missileEntity, out var missileTransform)) return;
                if (!PIDStateLookup.TryGetComponent(missileEntity, out var pidState)) return;

                // Grab the winning clip's animated component data to know WHICH target to look at
                // (Since target Entity isn't easily blendable, we assume the highest weight clip dictates the target)
                var activeTargetEntity = Entity.Null;
                var useReactionTarget = false;
                var isLocalOffset = false;

                // Extract targeting logic from the highest weight clip (Value1)
                // Note: In a production environment with complex targeting blends, you might want to 
                // handle this via a dedicated TargetTrack.
                // For simplicity, we just seek the active target.

                // Resolve blended PID values
                var blendedPID = JobHelpers.Blend<PhysicsPIDData, PhysicsPIDMixer>(ref mixData, default);

                // --- RESOLVE TARGET POSITION ---
                // We need the raw Authored data for non-blendable flags (like Target Entity).
                // We can't easily extract it from MixData, so we will look for the target from Targets.
                var finalTargetEntity = Entity.Null;
                if (TargetsLookup.TryGetComponent(missileEntity, out var reactionTargets))
                {
                    finalTargetEntity = reactionTargets.Target;
                }

                if (finalTargetEntity == Entity.Null || !LocalTransformLookup.TryGetComponent(finalTargetEntity, out var targetTransform))
                {
                    return; // No valid target to seek
                }

                // Calculate Desired Position
                var desiredPosition = targetTransform.Position;
                
                // Assume Local Offset is standard for missiles (e.g. "go 10 units behind the target")
                // A more advanced system would fetch the exact IsLocalOffset flag from the clip.
                var worldOffset = math.rotate(targetTransform.Rotation, blendedPID.Offset);
                desiredPosition += worldOffset;

                // --- PID MATH ---
                var error = desiredPosition - missileTransform.Position;

                if (!pidState.IsInitialized)
                {
                    pidState.PreviousError = error;
                    pidState.IntegralAccumulator = float3.zero;
                    pidState.IsInitialized = true;
                }

                // Integral (Builds up over time if blocked)
                pidState.IntegralAccumulator += error * DeltaTime;

                // Derivative (Dampens speed as it approaches)
                var derivative = (error - pidState.PreviousError) / DeltaTime;
                pidState.PreviousError = error;

                // Calculate Force
                var force = (blendedPID.Proportional * error) 
                          + (blendedPID.Integral * pidState.IntegralAccumulator) 
                          + (blendedPID.Derivative * derivative);

                // Clamp Force
                var forceMag = math.length(force);
                if (forceMag > blendedPID.MaxForce)
                {
                    force = (force / forceMag) * blendedPID.MaxForce;
                }

                // Apply Force as acceleration (dv = F * dt)
                velocity.Linear += force * DeltaTime;

                // Write back states
                PhysicsVelocityLookup[missileEntity] = velocity;
                PIDStateLookup[missileEntity] = pidState;
            }
        }
    }
}