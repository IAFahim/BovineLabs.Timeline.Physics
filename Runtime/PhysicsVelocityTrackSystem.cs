using BovineLabs.Timeline.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsVelocityTrackSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsVelocityComponent, TrackBinding>()
                .WithAny<ClipActive, ClipActivePrevious>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new PhysicsVelocityTrackSystemJob
            {
                PhysicsVelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAny(typeof(ClipActive), typeof(ClipActivePrevious))]
        public partial struct PhysicsVelocityTrackSystemJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsVelocity> PhysicsVelocityLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public float DeltaTime;

            public void Execute(in PhysicsVelocityComponent physicsVelocityComponent, in TrackBinding trackBinding)
            {
                if (!PhysicsVelocityLookup.HasComponent(trackBinding.Value)) return;

                var velocity = PhysicsVelocityLookup.GetRefRW(trackBinding.Value);

                var linear = physicsVelocityComponent.PhysicsVelocity.Linear;
                var angular = physicsVelocityComponent.PhysicsVelocity.Angular;

                if (physicsVelocityComponent.IsLocalSpace && LocalTransformLookup.HasComponent(trackBinding.Value))
                {
                    var rot = LocalTransformLookup[trackBinding.Value].Rotation;
                    linear = math.rotate(rot, linear);
                    angular = math.rotate(rot, angular);
                }

                velocity.ValueRW.Linear += linear * DeltaTime;
                velocity.ValueRW.Angular += angular * DeltaTime;
            }
        }
    }
}
