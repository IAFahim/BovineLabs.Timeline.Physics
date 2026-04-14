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

            // Zero-allocation iteration through bindings during baking
            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsPIDAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target != Entity.Null && !SystemAPI.HasComponent<PhysicsPIDState>(target))
                    ecb.AddComponent<PhysicsPIDState>(target);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}