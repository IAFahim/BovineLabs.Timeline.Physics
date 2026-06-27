using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// The second motion channel: external velocity (knockback, blasts, anything that should push a body around
    /// <em>independently</em> of its own locomotion). It is standing state, NOT a per-frame buffer — it persists
    /// and decays on its own schedule (<c>external-velocity.decay-rate</c>), so a brake/drag/reset acting on the
    /// intent channel (the regular <see cref="PhysicsVelocity"/>) can never eat an incoming hit.
    /// </summary>
    /// <remarks>
    /// Each fixed step it is composited into <see cref="PhysicsVelocity"/> just before the solver
    /// (<c>PhysicsExternalVelocityComposeSystem</c>) so collisions respond to the hit, then subtracted straight back
    /// out at the top of the post-solve modifier group (<c>PhysicsExternalVelocityDecomposeSystem</c>) so drag,
    /// override, clamp and reset only ever see locomotion. Fed by the <see cref="PendingExternalForce"/> inbox.
    /// </remarks>
    public struct ExternalVelocity : IComponentData
    {
        public float3 Linear;
        public float3 Angular;
    }
}
