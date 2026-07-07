using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsAngularPIDData
    {
        public PidTuning Tuning;
        public Target TrackingTarget;
        public PidAngularTargetMode TargetMode;

        public quaternion TargetRotation;

        public float Strength;
        public StatSource StrengthStat;
    }

    public struct PhysicsAngularPIDAnimated : IAnimatedComponent<PhysicsAngularPIDData>, IPreparable
    {
        public PhysicsAngularPIDData AuthoredData;
        [CreateProperty] public PhysicsAngularPIDData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveAngularPid : IActive<PhysicsAngularPIDData>
    {
        public PhysicsAngularPIDData Config { get; set; }
    }

    public struct PhysicsAngularPIDState : IComponentData, IDrainableLatchState<PhysicsAngularPIDData>
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

        // See PhysicsLinearPIDState.IsDrained: a controller is never one-shot-done, so an orphaned latch lingers
        // exactly one fixed tick to guarantee a short clip is never a silent no-op.
        public bool IsDrained(in PhysicsAngularPIDData config)
        {
            return false;
        }
    }

    public readonly struct PhysicsAngularPIDMixer : IMixer<PhysicsAngularPIDData>
    {
        private static quaternion SanitizeRotation(in quaternion q)
        {
            return math.lengthsq(q.value) > 1e-6f ? math.normalize(q) : quaternion.identity;
        }

        public PhysicsAngularPIDData Lerp(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b, in float s)
        {
            return new PhysicsAngularPIDData
            {
                Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
                TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
                TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
                TargetRotation = math.slerp(SanitizeRotation(a.TargetRotation), SanitizeRotation(b.TargetRotation), s),
                Strength = math.lerp(a.Strength, b.Strength, s),
                StrengthStat = s < 0.5f ? a.StrengthStat : b.StrengthStat
            };
        }

        public PhysicsAngularPIDData Add(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b)
        {
            var aWins = a.Strength > b.Strength ||
                        (a.Strength == b.Strength && (byte)a.TargetMode <= (byte)b.TargetMode);
            var dominant = aWins ? a : b;

            return new PhysicsAngularPIDData
            {
                Tuning = PidMixer.Add(a.Tuning, b.Tuning),
                TrackingTarget = dominant.TrackingTarget,
                TargetMode = dominant.TargetMode,
                TargetRotation =
                    PidMixer.AddRotation(SanitizeRotation(a.TargetRotation), SanitizeRotation(b.TargetRotation)),
                Strength = a.Strength + b.Strength,
                StrengthStat = dominant.StrengthStat
            };
        }
    }
}