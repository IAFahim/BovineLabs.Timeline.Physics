using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsTimelineBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsLinearPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsLinearPIDState>(target))
                {
                    ecb.AddComponent<ActiveLinearPid>(target);
                    ecb.SetComponentEnabled<ActiveLinearPid>(target, false);
                    ecb.AddComponent<PhysicsLinearPIDState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsAngularPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsAngularPIDState>(target))
                {
                    ecb.AddComponent<ActiveAngularPid>(target);
                    ecb.SetComponentEnabled<ActiveAngularPid>(target, false);
                    ecb.AddComponent<PhysicsAngularPIDState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsForceAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveForce>(target))
                {
                    ecb.AddComponent<ActiveForce>(target);
                    ecb.SetComponentEnabled<ActiveForce>(target, false);
                    ecb.AddComponent<PhysicsForceState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsVelocityAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveVelocity>(target))
                {
                    ecb.AddComponent<ActiveVelocity>(target);
                    ecb.SetComponentEnabled<ActiveVelocity>(target, false);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsDragAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveDrag>(target))
                {
                    ecb.AddComponent<ActiveDrag>(target);
                    ecb.SetComponentEnabled<ActiveDrag>(target, false);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}