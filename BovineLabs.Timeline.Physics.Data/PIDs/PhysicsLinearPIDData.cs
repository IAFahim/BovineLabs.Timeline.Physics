using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
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
        public StatStrengthConfig StrengthStat;
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

    public struct PhysicsLinearPIDState : IComponentData
    {
        public PidStateData State;
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