using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsPIDBakingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsPIDAnimated>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build();

            var bindings = query.ToComponentDataArray<TrackBinding>(Allocator.Temp);
            
            foreach (var binding in bindings)
            {
                if (binding.Value != Entity.Null && !state.EntityManager.HasComponent<PhysicsPIDState>(binding.Value))
                {
                    state.EntityManager.AddComponent<PhysicsPIDState>(binding.Value);
                }
            }
            
            bindings.Dispose();
        }
    }
}