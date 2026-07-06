using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
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

        /// <summary>
        /// Which velocity channel this force lands on. <see cref="MotionChannel.Intent"/> (default) is shaped by the
        /// body's own drag/clamp/reset; <see cref="MotionChannel.External"/> is knockback that survives braking and
        /// self-decays. Independent of <see cref="Mode"/> (impulse-vs-continuous).
        /// </summary>
        public MotionChannel Channel;

        public float3 Linear;
        public Target Space;

        public float Magnitude;
        public EntityLinkRef DirectionTarget;

        public float ConeAzimuthCenter;

        public float ConeAzimuthHalfRange;
        public float ConeElevationCenter;
        public float ConeElevationHalfRange;

        public uint Seed;

        public bool LatchDirection;

        public VelocityResetFlags ResetVelocityOnFire;

        public float3 Angular;
        public StatSource Strength;

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. Without it,
        // default(PhysicsForceData) in a low-weight slot silently wins the discrete fields (Mode = Continuous,
        // DirectionMode = FixedVector, Channel = Intent, default Space/Seed/Latch/Reset/Strength) — so an eased
        // Impulse clip integrates a spurious continuous force during its prelude. The mixer keys off this flag to
        // pull every discrete field from the PRESENT side.
        public byte Present;
    }

    public struct PhysicsForceState : IComponentData, IElapsedTimeState
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

        float IElapsedTimeState.ElapsedTime
        {
            get => ElapsedTime;
            set => ElapsedTime = value;
        }
    }

    public struct PhysicsForceRandom : IComponentData
    {
        public Random Value;
    }

    public struct PhysicsForceAnimated : IAnimatedComponent<PhysicsForceData>, IPreparable
    {
        public PhysicsForceData AuthoredData;
        [CreateProperty] public PhysicsForceData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveForce : IActive<PhysicsForceData>
    {
        public PhysicsForceData Config { get; set; }
    }

    public readonly struct PhysicsForceMixer : IMixer<PhysicsForceData>
    {
        public PhysicsForceData Lerp(in PhysicsForceData a, in PhysicsForceData b, in float s)
        {
            // Numeric fields still lerp (fading Linear/Magnitude toward 0 at low weight is correct fade semantics),
            // but every DISCRETE field must come from the present side: an empty slot's default Continuous/FixedVector
            // /Intent must never win, else an eased Impulse clip integrates a spurious continuous force at low weight.
            var aDefault = a.Present == 0;
            var bDefault = b.Present == 0;
            var discrete = bDefault || (!aDefault && s < 0.5f) ? a : b;

            return new PhysicsForceData
            {
                Mode = discrete.Mode,
                DirectionMode = discrete.DirectionMode,
                Channel = discrete.Channel,
                Linear = math.lerp(a.Linear, b.Linear, s),
                Space = discrete.Space,
                Magnitude = math.lerp(a.Magnitude, b.Magnitude, s),
                DirectionTarget = discrete.DirectionTarget,
                ConeAzimuthCenter = math.lerp(a.ConeAzimuthCenter, b.ConeAzimuthCenter, s),
                ConeAzimuthHalfRange = math.lerp(a.ConeAzimuthHalfRange, b.ConeAzimuthHalfRange, s),
                ConeElevationCenter = math.lerp(a.ConeElevationCenter, b.ConeElevationCenter, s),
                ConeElevationHalfRange = math.lerp(a.ConeElevationHalfRange, b.ConeElevationHalfRange, s),
                Seed = discrete.Seed,
                LatchDirection = discrete.LatchDirection,
                ResetVelocityOnFire = discrete.ResetVelocityOnFire,
                Angular = math.lerp(a.Angular, b.Angular, s),
                Strength = discrete.Strength,
                Present = (byte)(a.Present | b.Present)
            };
        }

        public PhysicsForceData Add(in PhysicsForceData a, in PhysicsForceData b)
        {
            // The additive fold calls Add(defaultValue, result) with an empty defaultValue in slot a, so pull the
            // discrete fields from whichever side is actually present rather than blindly from a.
            var discrete = a.Present != 0 ? a : b;

            return new PhysicsForceData
            {
                Mode = discrete.Mode,
                DirectionMode = discrete.DirectionMode,
                Channel = discrete.Channel,
                Linear = a.Linear + b.Linear,
                Space = discrete.Space,
                Magnitude = a.Magnitude + b.Magnitude,
                DirectionTarget = discrete.DirectionTarget,
                ConeAzimuthCenter = a.ConeAzimuthCenter + b.ConeAzimuthCenter,
                ConeAzimuthHalfRange = a.ConeAzimuthHalfRange + b.ConeAzimuthHalfRange,
                ConeElevationCenter = a.ConeElevationCenter + b.ConeElevationCenter,
                ConeElevationHalfRange = a.ConeElevationHalfRange + b.ConeElevationHalfRange,
                Seed = discrete.Seed,
                LatchDirection = discrete.LatchDirection,
                ResetVelocityOnFire = discrete.ResetVelocityOnFire,
                Angular = a.Angular + b.Angular,
                Strength = discrete.Strength,
                Present = (byte)(a.Present | b.Present)
            };
        }
    }
}