using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BovineLabs/Physics/Physics Force Accumulator")]
    public class PhysicsForceAccumulatorAuthoring : MonoBehaviour
    {
        private class Baker : Baker<PhysicsForceAccumulatorAuthoring>
        {
            public override void Bake(PhysicsForceAccumulatorAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddBuffer<PendingForce>(entity);
                AddBuffer<PendingVelocity>(entity);
            }
        }
    }
}