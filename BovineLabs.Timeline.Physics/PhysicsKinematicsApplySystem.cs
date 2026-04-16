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
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsKinematicsApplySystem : ISystem
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

            state.Dependency = new ApplyForceJob
            {
                DeltaTime = dt,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomLookup,
                TransformLookup = transformLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplyVelocityJob
            {
                DeltaTime = dt,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomLookup,
                TransformLookup = transformLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ApplyForceJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity entity, ref PhysicsVelocity velocity, in ActiveForce active, in LocalTransform transform, in PhysicsMass mass)
            {
                if (PhysicsMath.TryResolveSpaceVector(active.Config.Space, active.Config.Linear, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var linForce) &&
                    PhysicsMath.TryResolveSpaceVector(active.Config.Space, active.Config.Angular, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var angForce) &&
                    PhysicsMath.TryApplyLinearForce(velocity, mass, linForce, DeltaTime, out var v1) &&
                    PhysicsMath.TryApplyAngularTorque(v1, mass, transform, angForce, DeltaTime, out var v2))
                {
                    velocity = v2;
                }
            }
        }

        [BurstCompile]
        private partial struct ApplyVelocityJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            private void Execute(Entity entity, ref PhysicsVelocity velocity, in ActiveVelocity active)
            {
                if (PhysicsMath.TryResolveSpaceVector(active.Config.Space, active.Config.Linear, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var linVel) &&
                    PhysicsMath.TryResolveSpaceVector(active.Config.Space, active.Config.Angular, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var angVel))
                {
                    velocity.Linear += linVel * DeltaTime;
                    velocity.Angular += angVel * DeltaTime;
                }
            }
        }
    }
}
