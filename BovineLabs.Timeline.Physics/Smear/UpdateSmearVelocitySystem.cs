using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Smear
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct UpdateSmearVelocitySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UpdateSmearJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct UpdateSmearJob : IJobEntity
        {
            private void Execute(ref SmearVelocity smearVel, in PhysicsVelocity physicsVel)
            {
                smearVel.Value = new float4(physicsVel.Linear, 0f);
            }
        }
    }
}