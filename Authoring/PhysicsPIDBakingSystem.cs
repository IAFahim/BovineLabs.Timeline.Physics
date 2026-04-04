using BovineLabs.Timeline.Data;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [RequireMatchingQueriesForUpdate]
    public partial struct PhysicsPIDBakingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsPIDAnimated>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var targetsToAdd = new NativeList<Entity>(16, Allocator.Temp);

            foreach (var trackBinding in SystemAPI.Query<RefRO<TrackBinding>>()
                .WithAll<PhysicsPIDAnimated>()
                .WithOptions(
                    EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = trackBinding.ValueRO.Value;
                if (target == Entity.Null)
                {
                    continue;
                }

                if (!state.EntityManager.HasComponent<PhysicsPIDState>(target))
                {
                    targetsToAdd.Add(target);
                }
            }

            var em = state.EntityManager;
            for (var i = 0; i < targetsToAdd.Length; i++)
            {
                em.AddComponent<PhysicsPIDState>(targetsToAdd[i]);
            }

            targetsToAdd.Dispose();
        }
    }
}
