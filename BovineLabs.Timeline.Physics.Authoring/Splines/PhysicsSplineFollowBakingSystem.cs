using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    /// <summary>
    ///     Pre-adds the disabled <see cref="ActiveSplineFollow" /> + <see cref="PhysicsSplineFollowState" /> pair and
    ///     the force accumulation buffers to every body bound by a spline-follow track, mirroring
    ///     PhysicsTimelineBakingSystem. The track system enables the Active component only while a clip is live.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct PhysicsSplineFollowBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var binding in SystemAPI.Query<RefRO<TrackBinding>>()
                         .WithAll<PhysicsSplineFollowAnimated>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                var target = binding.ValueRO.Value;
                if (target == Entity.Null)
                {
                    continue;
                }

                if (!em.HasComponent<ActiveSplineFollow>(target))
                {
                    ecb.AddComponent<ActiveSplineFollow>(target);
                    ecb.SetComponentEnabled<ActiveSplineFollow>(target, false);
                    ecb.AddComponent<PhysicsSplineFollowState>(target);
                }

                if (!em.HasBuffer<PendingForce>(target))
                {
                    ecb.AddBuffer<PendingForce>(target);
                }

                if (!em.HasBuffer<PendingVelocity>(target))
                {
                    ecb.AddBuffer<PendingVelocity>(target);
                }

                if (!em.HasComponent<PendingVelocityReset>(target))
                {
                    ecb.AddComponent<PendingVelocityReset>(target);
                    ecb.SetComponentEnabled<PendingVelocityReset>(target, false);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
}
