namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Which velocity channel a force contribution lands on. <see cref="Intent"/> is the body's own locomotion
    /// (regular <see cref="Unity.Physics.PhysicsVelocity"/>) that brakes/drag/clamp are meant to shape;
    /// <see cref="External"/> is the knockback channel (<see cref="ExternalVelocity"/>) that survives braking and
    /// fades on its own. Choosing the channel is independent of impulse-vs-continuous (that is the force Mode).
    /// </summary>
    public enum MotionChannel : byte
    {
        /// <summary>Locomotion channel — drag/clamp/override/reset shape it. The default for designer-authored forces.</summary>
        Intent = 0,

        /// <summary>Knockback channel — immune to braking, decays on its own. For externally-sourced victim impulses/fields.</summary>
        External = 1,
    }
}
