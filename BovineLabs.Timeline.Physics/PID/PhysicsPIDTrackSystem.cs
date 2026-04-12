using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
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
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeComponentLookup<PhysicsVelocity> physicsVelocityLookup;
        private UnsafeComponentLookup<PhysicsPIDState> pidStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            this.localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(false);
            this.pidStateLookup = state.GetUnsafeComponentLookup<PhysicsPIDState>(false);
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

            this.localTransformLookup.Update(ref state);
            this.targetsLookup.Update(ref state);
            this.physicsVelocityLookup.Update(ref state);
            this.pidStateLookup.Update(ref state);

            state.Dependency = new PreparePIDDataJob().ScheduleParallel(state.Dependency);
            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new ApplyPIDVelocityJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = this.localTransformLookup,
                TargetsLookup = this.targetsLookup,
                PhysicsVelocityLookup = this.physicsVelocityLookup,
                PIDStateLookup = this.pidStateLookup
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
            public float DeltaTime;
            
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            
            public UnsafeComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            public UnsafeComponentLookup<PhysicsPIDState> PIDStateLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(this.BlendData, entryIndex, out var entity, out var mixData);

                if (!this.PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!this.LocalTransformLookup.TryGetComponent(entity, out var transform)) return;
                if (!this.PIDStateLookup.TryGetComponent(entity, out var pidState)) return;

                var blendedPID = JobHelpers.Blend<PhysicsPIDData, PhysicsPIDMixer>(ref mixData, default);

                var selfGoal = transform.Position + math.rotate(transform.Rotation, blendedPID.LocalTargetOffset);
                var finalGoal = selfGoal;

                if (blendedPID.ChaseTargetBlend > 0.001f && this.TargetsLookup.TryGetComponent(entity, out var targets))
                {
                    if (this.LocalTransformLookup.TryGetComponent(targets.Target, out var enemyTransform))
                    {
                        var enemyGoal = enemyTransform.Position + math.rotate(enemyTransform.Rotation, blendedPID.LocalTargetOffset);
                        finalGoal = math.lerp(selfGoal, enemyGoal, blendedPID.ChaseTargetBlend);
                    }
                }

                var error = finalGoal - transform.Position;

                if (!pidState.IsInitialized)
                {
                    pidState.PreviousError = error;
                    pidState.IntegralAccumulator = float3.zero;
                    pidState.IsInitialized = true;
                }

                pidState.IntegralAccumulator += error * this.DeltaTime;
                var derivative = (error - pidState.PreviousError) / this.DeltaTime;
                pidState.PreviousError = error;

                var force = (blendedPID.Proportional * error) 
                          + (blendedPID.Integral * pidState.IntegralAccumulator) 
                          + (blendedPID.Derivative * derivative);

                var forceMagSq = math.lengthsq(force);
                if (forceMagSq > (blendedPID.MaxForce * blendedPID.MaxForce))
                {
                    force = math.normalize(force) * blendedPID.MaxForce;
                }

                velocity.Linear += force * this.DeltaTime;

                this.PhysicsVelocityLookup[entity] = velocity;
                this.PIDStateLookup[entity] = pidState;
            }
        }
    }
}