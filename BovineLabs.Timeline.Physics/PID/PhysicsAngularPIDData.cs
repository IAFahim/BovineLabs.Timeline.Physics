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
        public float3 LocalTargetRotationEuler;
        public float ChaseTargetBlend;
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
            LocalTargetRotationEuler = math.lerp(a.LocalTargetRotationEuler, b.LocalTargetRotationEuler, s),
            ChaseTargetBlend = math.lerp(a.ChaseTargetBlend, b.ChaseTargetBlend, s)
        };

        public PhysicsAngularPIDData Add(in PhysicsAngularPIDData a, in PhysicsAngularPIDData b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Integral = a.Integral + b.Integral,
            Derivative = a.Derivative + b.Derivative,
            MaxTorque = a.MaxTorque + b.MaxTorque,
            LocalTargetRotationEuler = a.LocalTargetRotationEuler + b.LocalTargetRotationEuler,
            ChaseTargetBlend = a.ChaseTargetBlend + b.ChaseTargetBlend
        };
    }
}