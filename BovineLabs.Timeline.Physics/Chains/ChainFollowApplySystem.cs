using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Chains
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainFollowApplySystem : ISystem
    {
        private ComponentLookup<ActiveChainFollow> _followLookup;
        private ComponentLookup<LocalToWorld> _localToWorldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _followLookup = state.GetComponentLookup<ActiveChainFollow>(true);
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 1e-5f) return;

            _followLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);

            state.Dependency = new DriveJob
            {
                DeltaTime = dt,
                FollowLookup = _followLookup,
                LocalToWorldLookup = _localToWorldLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DriveJob : IJobEntity
        {
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<ActiveChainFollow> FollowLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;

            private void Execute(in ChainLink link, in PhysicsVelocity velocity, in LocalToWorld linkL2W,
                ref DynamicBuffer<PendingVelocity> pending)
            {
                if (!FollowLookup.HasComponent(link.Root) ||
                    !FollowLookup.IsComponentEnabled(link.Root)) return;

                if (!LocalToWorldLookup.TryGetComponent(link.AnimationBone, out var boneL2W)) return;

                var config = FollowLookup[link.Root].Config;

                var currentPosition = linkL2W.Position;
                var currentRotation = new quaternion(math.orthonormalize(new float3x3(linkL2W.Value)));
                var goalPosition = boneL2W.Position;
                var goalRotation = new quaternion(math.orthonormalize(new float3x3(boneL2W.Value)));

                SpringMath.CriticalSpring(currentPosition, velocity.Linear, goalPosition, float3.zero,
                    config.PositionHalflife, DeltaTime, out var nextPosition, out _);
                SpringMath.CriticalSpringRotation(currentRotation, velocity.Angular, goalRotation,
                    config.OrientationHalflife, DeltaTime, out var nextRotation, out _);

                var impliedLinear = ClampLength((nextPosition - currentPosition) / DeltaTime, config.MaxLinearSpeed);
                var impliedAngular = ClampLength(
                    SpringMath.Log(math.mul(nextRotation, math.conjugate(currentRotation))) / DeltaTime,
                    config.MaxAngularSpeed);

                pending.Add(new PendingVelocity
                {
                    Linear = (impliedLinear - velocity.Linear) * config.PositionStrength,
                    Angular = (impliedAngular - velocity.Angular) * config.OrientationStrength
                });
            }

            private static float3 ClampLength(float3 v, float maxLength)
            {
                var lengthSquared = math.lengthsq(v);
                if (lengthSquared <= maxLength * maxLength || lengthSquared < 1e-10f) return v;
                return v / math.sqrt(lengthSquared) * maxLength;
            }
        }
    }
}
