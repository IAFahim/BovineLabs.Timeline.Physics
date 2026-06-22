using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [InternalBufferCapacity(8)]
    public struct SweptTriggerHit : IBufferElementData
    {
        public Entity Value;
    }
}