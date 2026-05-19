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

    public struct PhysicsTriggerForceData : IComponentData
    {
        public StatefulEventState EventState;
        public PhysicsTriggerForceType ForceType;
        public PhysicsForceMode Mode;
        public float Magnitude;
        public float3 Direction;
        public PhysicsTriggerPositionMode OriginMode;

        public StatStrengthConfig Strength;

        public Target ApplyTo;
        public ushort ApplyToLinkKey;
    }
}