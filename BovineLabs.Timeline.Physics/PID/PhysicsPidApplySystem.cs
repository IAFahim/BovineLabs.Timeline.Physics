using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]
    public partial struct PhysicsPidApplySystem : ISystem
    {
        private UnsafeComponentLookup<Targets> targetsLookup;
        private ComponentLookup<TargetsCustom> targetsCustomLookup;
        private UnsafeComponentLookup<LocalTransform> transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            targetsLookup.Update(ref state);
            targetsCustomLookup.Update(ref state);
            transformLookup.Update(ref state);

            state.Dependency = new ApplyLinearJob
            {
                DeltaTime = dt,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomLookup,
                TransformLookup = transformLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyAngularJob
            {
                DeltaTime = dt,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomLookup,
                TransformLookup = transformLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ApplyLinearJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity entity, ref PhysicsVelocity velocity, ref PhysicsLinearPIDState state, in ActiveLinearPid active, in LocalTransform transform, in PhysicsMass mass)
            {
                if (PhysicsMath.TryResolveLinearPidTarget(transform, active.Config, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var targetPos) &&
                    PhysicsMath.TryCalculatePid(transform.Position - targetPos, active.Config.Tuning, state.State, DeltaTime, out var force, out var nextState) &&
                    PhysicsMath.TryApplyLinearForce(velocity, mass, -force, DeltaTime, out var nextVelocity))
                {
                    velocity = nextVelocity;
                    state.State = nextState;
                }
            }
        }

        [BurstCompile]
        private partial struct ApplyAngularJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity entity, ref PhysicsVelocity velocity, ref PhysicsAngularPIDState state, in ActiveAngularPid active, in LocalTransform transform, in PhysicsMass mass)
            {
                if (PhysicsMath.TryResolveAngularPidTarget(transform, active.Config, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var targetRot) &&
                    PhysicsMath.TryCalculateAngularError(transform.Rotation, targetRot, out var error) &&
                    PhysicsMath.TryCalculatePid(error, active.Config.Tuning, state.State, DeltaTime, out var torque, out var nextState) &&
                    PhysicsMath.TryApplyAngularTorque(velocity, mass, transform, torque, DeltaTime, out var nextVelocity))
                {
                    velocity = nextVelocity;
                    state.State = nextState;
                }
            }
        }
    }
}
