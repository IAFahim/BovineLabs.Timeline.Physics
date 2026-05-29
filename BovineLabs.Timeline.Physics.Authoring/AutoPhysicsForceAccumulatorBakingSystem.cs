using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Add this to a GameObject to prevent the PhysicsForceAccumulatorBakingSystem from automatically
    /// adding PendingForce and PendingVelocity buffers to its physics body.
    /// </summary>
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

    public struct PhysicsForceAccumulatorOptOut : IComponentData { }

    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    public partial struct AutoPhysicsForceAccumulatorBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Automatically add PendingForce and PendingVelocity to any dynamic physics body
            // that doesn't explicitly opt out, and hasn't already received them.
            foreach (var (_, entity) in SystemAPI.Query<RefRO<PhysicsVelocity>>()
                         .WithNone<PhysicsForceAccumulatorOptOut, PendingForce>()
                         .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                         .WithEntityAccess())
            {
                ecb.AddBuffer<PendingForce>(entity);
                ecb.AddBuffer<PendingVelocity>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
