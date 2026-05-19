using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [InternalBufferCapacity(4)]
    public struct PendingForce : IBufferElementData
    {
        public float3 Linear;
        public float3 Angular;
    }
}