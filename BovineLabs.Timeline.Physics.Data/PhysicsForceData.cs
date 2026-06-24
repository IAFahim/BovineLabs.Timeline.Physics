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

        RandomSphere,

        RandomCone,

        AlongVelocity,

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

        public float ConeAzimuthCenter;

        public float ConeAzimuthHalfRange;
        public float ConeElevationCenter;
        public float ConeElevationHalfRange;

        public uint Seed;

        public bool LatchDirection;

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

        /// <summary>
        /// Total CLIP-active time accumulated by the (render-rate) track system since this clip activated.
        /// A Continuous force integrates against the delta of this — not the fixed-step DeltaTime — so the total
        /// impulse is force × active-duration regardless of how many fixed steps land in the window. Without it,
        /// a Continuous force is force × fixedDt × (fixed-steps-in-window), and that step count jitters run-to-run
        /// (the fixed-step group ticks a variable number of times per rendered frame) — making continuous dashes
        /// non-deterministic while impulse (a one-shot latch) stays reliable.
        /// </summary>
        public float ElapsedTime;

        /// <summary>The portion of <see cref="ElapsedTime"/> already converted to impulse by the consumer.</summary>
        public float AppliedTime;
    }

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