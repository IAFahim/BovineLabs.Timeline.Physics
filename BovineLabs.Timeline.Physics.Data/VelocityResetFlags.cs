using System;

namespace BovineLabs.Timeline.Physics
{
    [Flags]
    public enum VelocityResetFlags : byte
    {
        None = 0,
        Linear = 1 << 0,
        Angular = 1 << 1,
        Both = Linear | Angular,

        /// <summary>
        /// Opt-in: also clear the <see cref="ExternalVelocity"/> (knockback) channel. By default a reset only zeroes
        /// the intent channel and an incoming hit survives — set this on moves that SHOULD eat knockback (hard parry,
        /// super-armor cancel).
        /// </summary>
        External = 1 << 2,
        All = Linear | Angular | External,
    }
}