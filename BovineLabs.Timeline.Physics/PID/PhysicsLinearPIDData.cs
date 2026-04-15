using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsLinearPIDData
    {
        public float3 Proportional;
        public float3 Integral;
        public float3 Derivative;
        public float MaxForce;
        
        public Target TrackingTarget;
        public PidLinearTargetMode TargetMode;
        public float3 TargetOffset;
    }

    public struct PhysicsLinearPIDAnimated : IAnimatedComponent<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData AuthoredData;
        [CreateProperty] public PhysicsLinearPIDData Value { get; set; }
    }

    public struct PhysicsLinearPIDState : IComponentData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public bool IsInitialized;
    }

    public readonly struct PhysicsLinearPIDMixer : IMixer<PhysicsLinearPIDData>
    {
        public PhysicsLinearPIDData Lerp(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b, in float s) => new()
        {
            Proportional = math.lerp(a.Proportional, b.Proportional, s),
            Integral = math.lerp(a.Integral, b.Integral, s),
            Derivative = math.lerp(a.Derivative, b.Derivative, s),
            MaxForce = math.lerp(a.MaxForce, b.MaxForce, s),
            TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
            TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
            TargetOffset = math.lerp(a.TargetOffset, b.TargetOffset, s)
        };

        public PhysicsLinearPIDData Add(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Integral = a.Integral + b.Integral,
            Derivative = a.Derivative + b.Derivative,
            MaxForce = a.MaxForce + b.MaxForce,
            TrackingTarget = a.TrackingTarget,
            TargetMode = a.TargetMode,
            TargetOffset = a.TargetOffset + b.TargetOffset
        };
    }
}