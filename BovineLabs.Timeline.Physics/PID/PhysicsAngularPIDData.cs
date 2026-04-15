using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsAngularPIDData
    {
        public float3 Proportional;
        public float3 Integral;
        public float3 Derivative;
        public float MaxTorque;

        public Target TrackingTarget;
        public PidAngularTargetMode TargetMode;
        public float3 TargetRotationEuler;
    }

    public struct PhysicsAngularPIDAnimated : IAnimatedComponent<PhysicsAngularPIDData>
    {
        public PhysicsAngularPIDData AuthoredData;
        [CreateProperty] public PhysicsAngularPIDData Value { get; set; }
    }

    public struct PhysicsAngularPIDState : IComponentData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public bool IsInitialized;
    }

    public readonly struct PhysicsAngularPIDMixer : IMixer<PhysicsAngularPIDData>
    {
        public PhysicsAngularPIDData Lerp(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b, in float s) => new()
        {
            Proportional = math.lerp(a.Proportional, b.Proportional, s),
            Integral = math.lerp(a.Integral, b.Integral, s),
            Derivative = math.lerp(a.Derivative, b.Derivative, s),
            MaxTorque = math.lerp(a.MaxTorque, b.MaxTorque, s),
            TrackingTarget = s < 0.5f ? a.TrackingTarget : b.TrackingTarget,
            TargetMode = s < 0.5f ? a.TargetMode : b.TargetMode,
            TargetRotationEuler = math.lerp(a.TargetRotationEuler, b.TargetRotationEuler, s)
        };

        public PhysicsAngularPIDData Add(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Integral = a.Integral + b.Integral,
            Derivative = a.Derivative + b.Derivative,
            MaxTorque = a.MaxTorque + b.MaxTorque,
            TrackingTarget = a.TrackingTarget,
            TargetMode = a.TargetMode,
            TargetRotationEuler = a.TargetRotationEuler + b.TargetRotationEuler
        };
    }
}