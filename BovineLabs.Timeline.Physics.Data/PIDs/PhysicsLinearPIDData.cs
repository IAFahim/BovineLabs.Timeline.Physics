using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsLinearPIDData
    {
        public PidTuning Tuning;
        public Target TrackingTarget;
        public PidLinearTargetMode TargetMode;

        public float3 TargetOffset;

        public float Strength;
        public StatSource StrengthStat;
    }

    public struct PhysicsLinearPIDAnimated : IAnimatedComponent<PhysicsLinearPIDData>, IPreparable
    {
        public PhysicsLinearPIDData AuthoredData;
        [CreateProperty] public PhysicsLinearPIDData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveLinearPid : IActive<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData Config { get; set; }
    }

    public struct PhysicsLinearPIDState : IComponentData, IDrainableLatchState<PhysicsLinearPIDData>
    {
        public PidStateData State;

        /// <summary>
        /// Fixed-step drain gate: set when the render-side stale-disable lingers this latch enabled so a PID clip
        /// whose enable window straddled no fixed tick still gets one control tick before it is disabled. Mirrors
        /// PhysicsForceState.
        /// </summary>
        public bool Orphaned;

        bool IOrphanedLatch.Orphaned
        {
            get => Orphaned;
            set => Orphaned = value;
        }

        // A PID controller has no one-shot terminal state and no accumulated tail: it just needs to be observed by at
        // least one fixed tick. Report never-drained so an orphaned latch always lingers exactly one fixed tick (the
        // drain-finalize then disables it), guaranteeing a short PID clip is never a silent no-op.
        public bool IsDrained(in PhysicsLinearPIDData config)
        {
            return false;
        }
    }

    public readonly struct PhysicsLinearPIDMixer : IMixer<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData Lerp(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b, in float s)
        {
            var aAbsolute = IsAbsolute(a.TargetMode);
            var bAbsolute = IsAbsolute(b.TargetMode);
            if (aAbsolute != bAbsolute)
            {
                var absolute = aAbsolute ? a : b;
                return new PhysicsLinearPIDData
                {
                    Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
                    TrackingTarget = absolute.TrackingTarget,
                    TargetMode = absolute.TargetMode,
                    TargetOffset = absolute.TargetOffset,
                    Strength = math.lerp(a.Strength, b.Strength, s),
                    StrengthStat = absolute.StrengthStat
                };
            }

            return new PhysicsLinearPIDData
            {
                Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
                TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
                TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
                TargetOffset = math.lerp(a.TargetOffset, b.TargetOffset, s),
                Strength = math.lerp(a.Strength, b.Strength, s),
                StrengthStat = s < 0.5f ? a.StrengthStat : b.StrengthStat
            };
        }

        private static bool IsAbsolute(PidLinearTargetMode mode)
        {
            return mode == PidLinearTargetMode.World || mode == PidLinearTargetMode.LineOfSight;
        }

        public PhysicsLinearPIDData Add(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b)
        {
            var aWins = a.Strength > b.Strength ||
                        (a.Strength == b.Strength && (byte)a.TargetMode <= (byte)b.TargetMode);
            var dominant = aWins ? a : b;

            return new PhysicsLinearPIDData
            {
                Tuning = PidMixer.Add(a.Tuning, b.Tuning),
                TrackingTarget = dominant.TrackingTarget,
                TargetMode = dominant.TargetMode,
                TargetOffset = a.TargetOffset + b.TargetOffset,
                Strength = a.Strength + b.Strength,
                StrengthStat = dominant.StrengthStat
            };
        }
    }
}