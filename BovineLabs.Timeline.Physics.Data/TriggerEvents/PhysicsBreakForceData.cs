using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsBreakMode : byte
    {
        /// <summary>Kill (Restitution 0) or reverse (Restitution &gt; 0) the contacted body's momentum along its own velocity.</summary>
        Brake,

        /// <summary>Send the contacted body off along an azimuth/elevation angle in the SOURCE's frame, at Restitution × incoming speed.</summary>
        Redirect,
    }

    /// <summary>
    ///     A "break force" clip on the Stateful Trigger track: when a body enters/stays/exits the bound trigger
    ///     SOURCE, measure its momentum and — if it is at or under a stat-gated threshold — apply a counter-impulse
    ///     that stops, reverses, or redirects it. Over threshold = it breaks through (no counter-force). Think of an
    ///     arrow hitting a shield whose strength is a live stat.
    /// </summary>
    public struct PhysicsBreakForceData : IComponentData
    {
        public StatefulEventState EventState;
        public PhysicsBreakMode Mode;

        /// <summary>Base momentum (|v|·mass) the break can absorb. Multiplied by the resolved <see cref="Strength" /> stat.</summary>
        public float BaseThreshold;

        /// <summary>0 = dead stop · 1 = full reverse / full-speed return · &gt;1 = amplified return.</summary>
        public float Restitution;

        /// <summary>Redirect mode: angle around the source's up axis (radians).</summary>
        public float Azimuth;

        /// <summary>Redirect mode: angle above the source's forward (radians).</summary>
        public float Elevation;

        /// <summary>Scales <see cref="BaseThreshold" /> — the "shield strength" stat, resolved through Targets/EntityLinks.</summary>
        public StatStrengthConfig Strength;

        /// <summary>Whom to push. Default <see cref="Target.Target" /> = the contacted body (the arrow).</summary>
        public Target ApplyTo;
        public ushort ApplyToLinkKey;
    }
}
