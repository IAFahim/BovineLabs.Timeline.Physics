using BovineLabs.Timeline.Data;
using Unity.Burst;
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

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsLinearPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsLinearPIDState>(target))
                {
                    em.AddComponent<ActiveLinearPid>(target);
                    em.SetComponentEnabled<ActiveLinearPid>(target, false);
                    em.AddComponent<PhysicsLinearPIDState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsAngularPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsAngularPIDState>(target))
                {
                    em.AddComponent<ActiveAngularPid>(target);
                    em.SetComponentEnabled<ActiveAngularPid>(target, false);
                    em.AddComponent<PhysicsAngularPIDState>(target);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsForceAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveForce>(target))
                {
                    em.AddComponent<ActiveForce>(target);
                    em.SetComponentEnabled<ActiveForce>(target, false);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsVelocityAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveVelocity>(target))
                {
                    em.AddComponent<ActiveVelocity>(target);
                    em.SetComponentEnabled<ActiveVelocity>(target, false);
                }
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsDragAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<ActiveDrag>(target))
                {
                    em.AddComponent<ActiveDrag>(target);
                    em.SetComponentEnabled<ActiveDrag>(target, false);
                }
            }
        }
    }
}