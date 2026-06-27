namespace Vex.Knockback
{
    using Unity.Entities;

    /// <summary>
    /// Marks a physics body ringed by stateful-trigger sphere zones (a compound of trigger leaves) that should be
    /// knocked back when one of those zones is touched. On a trigger <c>Enter</c>,
    /// <see cref="DirectionalKnockbackSystem"/> appends a one-shot <c>PendingForce</c> impulse pointing away — on the
    /// XZ plane — from the body that entered, plus a vertical "leap" component. Direction is derived from the contact
    /// geometry, so a touch on the front sphere knocks the body backward, a touch on the left knocks it right, and so
    /// on for all eight sides, with no per-zone configuration and correct behaviour even as the body rotates.
    /// </summary>
    public struct KnockbackReceiver : IComponentData
    {
        /// <summary>Horizontal impulse magnitude (N·s) applied away from the contact on the XZ plane.</summary>
        public float Strength;

        /// <summary>Vertical (+Y) impulse magnitude (N·s) applied on contact so the knockback reads as a leap.</summary>
        public float Lift;
    }
}
