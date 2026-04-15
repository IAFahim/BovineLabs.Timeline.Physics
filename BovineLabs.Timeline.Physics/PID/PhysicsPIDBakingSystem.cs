using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsPIDBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsLinearPIDAnimated>().WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsLinearPIDState>(target))
                    ecb.AddComponent<PhysicsLinearPIDState>(target);
            }

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>().WithAll<PhysicsAngularPIDAnimated>().WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsAngularPIDState>(target))
                    ecb.AddComponent<PhysicsAngularPIDState>(target);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}