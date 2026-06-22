using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsBreakMode : byte
    {
        Brake,

        Redirect
    }

    public struct PhysicsBreakForceData : IComponentData
    {
        public StatefulEventState EventState;
        public PhysicsBreakMode Mode;

        public float BaseThreshold;

        public float Restitution;

        public float Azimuth;

        public float Elevation;

        public StatStrengthConfig Strength;

        public Target ApplyTo;
        public ushort ApplyToLinkKey;
    }
}