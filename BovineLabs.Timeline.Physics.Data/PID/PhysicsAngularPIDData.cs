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
        public quaternion TargetRotation;
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
        public PhysicsAngularPIDData Lerp(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b, in float s) => new()
        {
            Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
            TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
            TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
            TargetRotation = math.slerp(a.TargetRotation, b.TargetRotation, s)
        };

        public PhysicsAngularPIDData Add(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b) => new()
        {
            Tuning = PidMixer.Add(a.Tuning, b.Tuning),
            TrackingTarget = a.TrackingTarget,
            TargetMode = a.TargetMode,
            TargetRotation = math.mul(a.TargetRotation, b.TargetRotation)
        };
    }
}
