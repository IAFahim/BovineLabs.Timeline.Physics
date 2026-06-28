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
    /// <para>
    /// Standing state (baked to zero). On fresh instantiation it is zero; it also self-heals because the decompose
    /// system continuously decays it to rest within the channel's lifetime (~&lt;1s at the default rate). If you POOL
    /// and recycle a dynamic body in-place mid-knockback, zero this (and clear the <see cref="PendingExternalForce"/>
    /// inbox) on reuse to avoid one frame of inherited velocity — e.g. in your InitializeEntity / pool-reset path.
    /// </para>
    /// </remarks>
    public struct ExternalVelocity : IComponentData
    {
        public float3 Linear;
        public float3 Angular;
    }
}
