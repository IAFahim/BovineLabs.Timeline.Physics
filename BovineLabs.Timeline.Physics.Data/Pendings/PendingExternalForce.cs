using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Per-frame inbox of impulses (force units, same as <see cref="PendingForce"/>) destined for the
    /// <see cref="ExternalVelocity"/> channel. Drained and mass-converted once per fixed step by
    /// <c>PhysicsExternalVelocityComposeSystem</c>, then cleared. Knockback writers append here instead of
    /// <see cref="PendingForce"/> so the resulting velocity rides the external channel and survives braking.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PendingExternalForce : IBufferElementData
    {
        public float3 Linear;
        public float3 Angular;
    }
}
