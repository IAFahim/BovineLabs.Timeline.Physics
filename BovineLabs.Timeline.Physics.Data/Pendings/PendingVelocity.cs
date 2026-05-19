using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [InternalBufferCapacity(0)]
    public struct PendingVelocity : IBufferElementData
    {
        public float3 Linear;
        public float3 Angular;
    }
}