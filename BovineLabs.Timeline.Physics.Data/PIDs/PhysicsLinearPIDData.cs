using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
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

        /// <summary>
        ///     In World mode, this acts as the absolute world position. In Offset mode, it is an offset from the tracking target.
        /// </summary>
        public float3 TargetOffset;

        public float Strength;
        public StatStrengthConfig StrengthStat;
    }

    public struct PhysicsLinearPIDAnimated : IAnimatedComponent<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData AuthoredData;
        [CreateProperty] public PhysicsLinearPIDData Value { get; set; }
    }

    public struct ActiveLinearPid : IComponentData, IEnableableComponent
    {
        public PhysicsLinearPIDData Config;
    }

    public struct PhysicsLinearPIDState : IComponentData
    {
        public PidStateData State;
    }

    public readonly struct PhysicsLinearPIDMixer : IMixer<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData Lerp(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b, in float s)
        {
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