namespace BovineLabs.Timeline.Physics
{
    using Unity.Entities;

    /// <summary>
    /// Per-source record of the entities the sweep overlapped LAST frame. Diffed against this frame's hits
    /// to derive Enter (new), Stay (still overlapping) and Exit (no longer overlapping) edges — mirroring
    /// the simulation's stateful trigger semantics. Maintained by <c>SweptTriggerSystem</c>.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SweptTriggerHit : IBufferElementData
    {
        public Entity Value;
    }
}
