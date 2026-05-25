using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateAfter(typeof(PhysicsTimelineBakingSystem))]
    public partial struct PhysicsTeleportBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var queuedTeleport = new NativeHashSet<Entity>(64, Allocator.Temp);
            var queuedBuffers = new NativeHashSet<Entity>(64, Allocator.Temp);

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsTeleportAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null) continue;

                if (!em.HasComponent<ActiveTeleport>(target) && queuedTeleport.Add(target))
                {
                    ecb.AddComponent<ActiveTeleport>(target);
                    ecb.SetComponentEnabled<ActiveTeleport>(target, false);
                    ecb.AddComponent<PhysicsTeleportState>(target);
                }

                if (queuedBuffers.Add(target))
                {
                    if (!em.HasBuffer<PendingForce>(target)) ecb.AddBuffer<PendingForce>(target);
                    if (!em.HasBuffer<PendingVelocity>(target)) ecb.AddBuffer<PendingVelocity>(target);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();

            queuedTeleport.Dispose();
            queuedBuffers.Dispose();
        }
    }
}