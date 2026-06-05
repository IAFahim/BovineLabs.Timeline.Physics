using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerForceType : byte
    {
        Directional, // Straight uniform push (conveyor belt, wind tunnels)
        Radial, // Pushes out or Pulls in from origin (Explosions/Implosions)
        Vortex // Spins things tangentially around the origin's Up axis
    }

    public enum PhysicsTriggerFalloffCurve : byte
    {
        None, // Constant force regardless of distance
        Linear, // Force decreases linearly from Start to End radius
        InverseSquare, // Realistic physical attenuation (1/r^2)
        Step // Full magnitude up to FalloffEndRadius, zero outside
    }

    public struct PhysicsTriggerForceData : IComponentData
    {
        public StatefulEventState EventState;
        public PhysicsTriggerForceType ForceType;
        public PhysicsForceMode Mode;
        public float Magnitude;
        public float3 Direction;

        /// <summary>
        ///     Note: Radial force combined with MatchCollidedEntity produces a zero-length direction.
        /// </summary>
        public PhysicsTriggerPositionMode OriginMode;

        public PhysicsTriggerFalloffCurve FalloffCurve;
        public float FalloffStartRadius;
        public float FalloffEndRadius;

        public StatStrengthConfig Strength;

        public Target ApplyTo;
        public ushort ApplyToLinkKey;
    }
}