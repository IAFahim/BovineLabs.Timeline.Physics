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
        AwayFromTarget,

        /// <summary>Uniform random direction on the unit sphere, scaled by Magnitude.</summary>
        RandomSphere,

        /// <summary>
        ///     Random direction inside an azimuth/elevation patch (same convention as the teleport
        ///     patch: azimuth 0 = +Z of the resolved Space frame), scaled by Magnitude.
        /// </summary>
        RandomCone,

        /// <summary>Along the body's current linear velocity, scaled by Magnitude. No-op while stationary.</summary>
        AlongVelocity,

        /// <summary>Against the body's current linear velocity, scaled by Magnitude. No-op while stationary.</summary>
        AgainstVelocity
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

        /// <summary>RandomCone patch, radians. The patch frame is the resolved Space rotation.</summary>
        public float ConeAzimuthCenter;

        public float ConeAzimuthHalfRange;
        public float ConeElevationCenter;
        public float ConeElevationHalfRange;

        /// <summary>Offsets the per-entity random stream. 0 is valid; entity identity decorrelates bodies.</summary>
        public uint Seed;

        /// <summary>
        ///     When true, stochastic and velocity-relative directions are sampled once per clip
        ///     activation and held; when false they are re-evaluated every fire.
        /// </summary>
        public bool LatchDirection;

        /// <summary>Zeroes the masked velocity axes once per clip activation, on the first fire tick.</summary>
        public VelocityResetFlags ResetVelocityOnFire;

        public float3 Angular;
        public StatStrengthConfig Strength;
    }

    public struct PhysicsForceState : IComponentData
    {
        public bool Fired;
        public bool ResetApplied;
        public bool DirectionLatched;
        public float3 LatchedDirection;
    }

    /// <summary>
    ///     Per-entity random stream for stochastic direction modes. Lives outside
    ///     <see cref="PhysicsForceState" /> so clip re-activation resets fire/latch state without
    ///     rewinding the stream — each activation draws fresh values. A zero state is lazily seeded
    ///     from (Seed, entity), so a given entity and seed always replays the same sequence.
    /// </summary>
    public struct PhysicsForceRandom : IComponentData
    {
        public Random Value;
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
                ConeAzimuthCenter = math.lerp(a.ConeAzimuthCenter, b.ConeAzimuthCenter, s),
                ConeAzimuthHalfRange = math.lerp(a.ConeAzimuthHalfRange, b.ConeAzimuthHalfRange, s),
                ConeElevationCenter = math.lerp(a.ConeElevationCenter, b.ConeElevationCenter, s),
                ConeElevationHalfRange = math.lerp(a.ConeElevationHalfRange, b.ConeElevationHalfRange, s),
                Seed = s < 0.5f ? a.Seed : b.Seed,
                LatchDirection = s < 0.5f ? a.LatchDirection : b.LatchDirection,
                ResetVelocityOnFire = s < 0.5f ? a.ResetVelocityOnFire : b.ResetVelocityOnFire,
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
                ConeAzimuthCenter = a.ConeAzimuthCenter + b.ConeAzimuthCenter,
                ConeAzimuthHalfRange = a.ConeAzimuthHalfRange + b.ConeAzimuthHalfRange,
                ConeElevationCenter = a.ConeElevationCenter + b.ConeElevationCenter,
                ConeElevationHalfRange = a.ConeElevationHalfRange + b.ConeElevationHalfRange,
                Seed = a.Seed,
                LatchDirection = a.LatchDirection,
                ResetVelocityOnFire = a.ResetVelocityOnFire,
                Angular = a.Angular + b.Angular,
                Strength = a.Strength
            };
        }
    }
}
