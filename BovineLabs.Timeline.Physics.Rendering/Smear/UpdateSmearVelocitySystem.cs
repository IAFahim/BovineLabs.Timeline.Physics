using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Smear
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct UpdateSmearVelocitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UpdateSmearJob
            {
                // Post-solve PhysicsVelocity is intent-only (the knockback channel is subtracted out in the modifier
                // group), so add ExternalVelocity back here or a knocked-back body would smear at walking speed.
                ExternalLookup = SystemAPI.GetComponentLookup<ExternalVelocity>(true),
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct UpdateSmearJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ExternalVelocity> ExternalLookup;

            private void Execute(Entity entity, ref SmearVelocity smearVel, in PhysicsVelocity physicsVel)
            {
                var linear = physicsVel.Linear;
                if (ExternalLookup.TryGetComponent(entity, out var external))
                {
                    linear += external.Linear;
                }

                smearVel.Value = new float4(linear, 0f);
            }
        }
    }
}