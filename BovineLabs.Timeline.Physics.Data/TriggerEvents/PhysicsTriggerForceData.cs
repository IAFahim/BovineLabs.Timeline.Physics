using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerForceType : byte
    {
        Directional,
        Radial,
        Vortex
    }

    public enum PhysicsTriggerFalloffCurve : byte
    {
        None,
        Linear,
        InverseSquare,
        Step
    }

    public struct PhysicsTriggerForceData : IComponentData
    {
        public StatefulEventState EventState;
        public PhysicsTriggerForceType ForceType;
        public PhysicsForceMode Mode;

        /// <summary>Intent (drag-shaped locomotion) or External (knockback that survives braking). Default Intent.</summary>
        public MotionChannel Channel;

        public float Magnitude;
        public float3 Direction;

        public PhysicsTriggerPositionMode OriginMode;

        public PhysicsTriggerFalloffCurve FalloffCurve;
        public float FalloffStartRadius;
        public float FalloffEndRadius;

        public StatStrengthConfig Strength;

        public Target ApplyTo;
        public ushort ApplyToLinkKey;
    }
}