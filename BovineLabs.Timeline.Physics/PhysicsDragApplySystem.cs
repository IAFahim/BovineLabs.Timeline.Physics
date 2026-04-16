using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsDragApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            state.Dependency = new ApplyDragJob
            {
                DeltaTime = dt
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ApplyDragJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            private void Execute(ref PhysicsVelocity velocity, in ActiveDrag active)
            {
                var linearMultiplier = math.clamp(1.0f - active.Config.Linear * DeltaTime, 0.0f, 1.0f);
                var angularMultiplier = math.clamp(1.0f - active.Config.Angular * DeltaTime, 0.0f, 1.0f);

                velocity.Linear *= linearMultiplier;
                velocity.Angular *= angularMultiplier;
            }
        }
    }
}
