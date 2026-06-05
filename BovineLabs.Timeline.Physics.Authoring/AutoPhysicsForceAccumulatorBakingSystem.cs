namespace BovineLabs.Timeline.Physics.Authoring
{
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Physics;
    using UnityEngine;

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
