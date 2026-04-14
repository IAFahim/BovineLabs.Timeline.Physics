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
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            physicsVelocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();
            pidStateLookup = state.GetUnsafeComponentLookup<PhysicsPIDState>();
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

            localTransformLookup.Update(ref state);
            targetsLookup.Update(ref state);
            physicsVelocityLookup.Update(ref state);
            pidStateLookup.Update(ref state);

            state.Dependency = new PreparePIDDataJob().ScheduleParallel(state.Dependency);
            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new ApplyPIDVelocityJob
            {
                BlendData = blendData,
                DeltaTime = dt,
                LocalTransformLookup = localTransformLookup,
                TargetsLookup = targetsLookup,
                PhysicsVelocityLookup = physicsVelocityLookup,
                PIDStateLookup = pidStateLookup
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
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!PhysicsVelocityLookup.TryGetComponent(entity, out var velocity)) return;
                if (!LocalTransformLookup.TryGetComponent(entity, out var transform)) return;
                if (!PIDStateLookup.TryGetComponent(entity, out var pidState)) return;

                var blendedPID = JobHelpers.Blend<PhysicsPIDData, PhysicsPIDMixer>(ref mixData, default);

                var selfGoal = transform.Position + math.rotate(transform.Rotation, blendedPID.LocalTargetOffset);
                var finalGoal = selfGoal;

                if (blendedPID.ChaseTargetBlend > 0.001f && TargetsLookup.TryGetComponent(entity, out var targets))
                    if (LocalTransformLookup.TryGetComponent(targets.Target, out var enemyTransform))
                    {
                        var enemyGoal = enemyTransform.Position +
                                        math.rotate(enemyTransform.Rotation, blendedPID.LocalTargetOffset);
                        finalGoal = math.lerp(selfGoal, enemyGoal, blendedPID.ChaseTargetBlend);
                    }

                var error = finalGoal - transform.Position;

                if (!pidState.IsInitialized)
                {
                    pidState.PreviousError = error;
                    pidState.IntegralAccumulator = float3.zero;
                    pidState.IsInitialized = true;
                }

                pidState.IntegralAccumulator += error * DeltaTime;
                var derivative = (error - pidState.PreviousError) / DeltaTime;
                pidState.PreviousError = error;

                var force = blendedPID.Proportional * error
                            + blendedPID.Integral * pidState.IntegralAccumulator
                            + blendedPID.Derivative * derivative;

                var forceMagSq = math.lengthsq(force);
                if (forceMagSq > blendedPID.MaxForce * blendedPID.MaxForce)
                    force = math.normalize(force) * blendedPID.MaxForce;

                velocity.Linear += force * DeltaTime;

                PhysicsVelocityLookup[entity] = velocity;
                PIDStateLookup[entity] = pidState;
            }
        }
    }
}