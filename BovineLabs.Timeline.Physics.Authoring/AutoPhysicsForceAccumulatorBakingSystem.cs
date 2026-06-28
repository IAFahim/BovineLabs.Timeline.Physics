using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BovineLabs/Physics/Physics Force Accumulator Opt-Out")]
    public class PhysicsForceAccumulatorOptOutAuthoring : MonoBehaviour
    {
        private class Baker : Baker<PhysicsForceAccumulatorOptOutAuthoring>
        {
            public override void Bake(PhysicsForceAccumulatorOptOutAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PhysicsForceAccumulatorOptOut>(entity);
            }
        }
    }

    /// <summary>
    /// Opts a body out of auto-provisioning by <see cref="AutoPhysicsForceAccumulatorBakingSystem"/>. This covers BOTH
    /// the intent channel (PendingForce/PendingVelocity) AND the external/knockback channel
    /// (PendingExternalForce/ExternalVelocity) — a self-managed body receives neither, and knockback writers will skip
    /// it (their HasBuffer guard). Add the buffers yourself if an opted-out body should still take knockback.
    /// </summary>
    public struct PhysicsForceAccumulatorOptOut : IComponentData
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct AutoPhysicsForceAccumulatorBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<PhysicsVelocity>>()
                         .WithNone<PhysicsForceAccumulatorOptOut>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                         .WithEntityAccess())
            {
                if (!em.HasBuffer<PendingForce>(entity)) ecb.AddBuffer<PendingForce>(entity);
                if (!em.HasBuffer<PendingVelocity>(entity)) ecb.AddBuffer<PendingVelocity>(entity);

                // External (knockback) channel: inbox buffer + standing velocity, default zero.
                if (!em.HasBuffer<PendingExternalForce>(entity)) ecb.AddBuffer<PendingExternalForce>(entity);
                if (!em.HasComponent<ExternalVelocity>(entity)) ecb.AddComponent<ExternalVelocity>(entity);

                if (!em.HasComponent<PendingVelocityReset>(entity))
                {
                    ecb.AddComponent<PendingVelocityReset>(entity);
                    ecb.SetComponentEnabled<PendingVelocityReset>(entity, false);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}