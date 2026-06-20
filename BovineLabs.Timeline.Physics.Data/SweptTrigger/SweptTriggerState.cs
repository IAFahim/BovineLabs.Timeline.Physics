namespace BovineLabs.Timeline.Physics
{
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Per-source sweep state: the world transform captured LAST frame (the start of this frame's sweep
    /// segment) and whether the source was active last frame (so a fresh activation does not sweep from a
    /// stale position). Written by <c>SweptTriggerSystem</c>.
    /// </summary>
    public struct SweptTriggerState : IComponentData
    {
        public float3 PrevPosition;
        public quaternion PrevRotation;

        /// <summary>1 if a clip was active on this source last frame, else 0.</summary>
        public byte WasActive;
    }
}
