using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsForceMode : byte
    {
        Continuous,
        Impulse
    }

    public enum PhysicsForceDirectionMode : byte
    {
        FixedVector,
        TowardTarget,
        AwayFromTarget
    }

    public struct PhysicsForceData
    {
        public PhysicsForceMode Mode;
        public PhysicsForceDirectionMode DirectionMode;

        public float3 Linear;
        public Target Space;

        public float Magnitude;
        public Target DirectionTarget;
        public ushort DirectionTargetLinkKey;

        public float3 Angular;
        public StatStrengthConfig Strength;
    }

    public struct PhysicsForceState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsForceAnimated : IAnimatedComponent<PhysicsForceData>
    {
        public PhysicsForceData AuthoredData;
        [CreateProperty] public PhysicsForceData Value { get; set; }
    }

    public struct ActiveForce : IComponentData, IEnableableComponent
    {
        public PhysicsForceData Config;
    }

    public readonly struct PhysicsForceMixer : IMixer<PhysicsForceData>
    {
        public PhysicsForceData Lerp(in PhysicsForceData a, in PhysicsForceData b, in float s)
        {
            return new PhysicsForceData
            {
                Mode = s < 0.5f ? a.Mode : b.Mode,
                DirectionMode = s < 0.5f ? a.DirectionMode : b.DirectionMode,
                Linear = math.lerp(a.Linear, b.Linear, s),
                Space = s < 0.5f ? a.Space : b.Space,
                Magnitude = math.lerp(a.Magnitude, b.Magnitude, s),
                DirectionTarget = s < 0.5f ? a.DirectionTarget : b.DirectionTarget,
                DirectionTargetLinkKey = s < 0.5f ? a.DirectionTargetLinkKey : b.DirectionTargetLinkKey,
                Angular = math.lerp(a.Angular, b.Angular, s),
                Strength = s < 0.5f ? a.Strength : b.Strength
            };
        }

        public PhysicsForceData Add(in PhysicsForceData a, in PhysicsForceData b)
        {
            return new PhysicsForceData
            {
                Mode = a.Mode,
                DirectionMode = a.DirectionMode,
                Linear = a.Linear + b.Linear,
                Space = a.Space,
                Magnitude = a.Magnitude + b.Magnitude,
                DirectionTarget = a.DirectionTarget,
                DirectionTargetLinkKey = a.DirectionTargetLinkKey,
                Angular = a.Angular + b.Angular,
                Strength = a.Strength
            };
        }
    }
}