using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
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

        /// <summary>
        ///     In World mode, this acts as the absolute world rotation. In Offset mode, it is an offset from the tracking target.
        /// </summary>
        public quaternion TargetRotation;

        public float Strength;
        public StatStrengthConfig StrengthStat;
    }

    public struct PhysicsAngularPIDAnimated : IAnimatedComponent<PhysicsAngularPIDData>
    {
        public PhysicsAngularPIDData AuthoredData;
        [CreateProperty] public PhysicsAngularPIDData Value { get; set; }
    }

    public struct ActiveAngularPid : IComponentData, IEnableableComponent
    {
        public PhysicsAngularPIDData Config;
    }

    public struct PhysicsAngularPIDState : IComponentData
    {
        public PidStateData State;
    }

    public readonly struct PhysicsAngularPIDMixer : IMixer<PhysicsAngularPIDData>
    {
        public PhysicsAngularPIDData Lerp(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b, in float s)
        {
            return new PhysicsAngularPIDData
            {
                Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
                TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
                TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
                TargetRotation = math.slerp(a.TargetRotation, b.TargetRotation, s),
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
                TargetRotation = PidMixer.AddRotation(a.TargetRotation, b.TargetRotation),
                Strength = a.Strength + b.Strength,
                StrengthStat = dominant.StrengthStat
            };
        }
    }
}