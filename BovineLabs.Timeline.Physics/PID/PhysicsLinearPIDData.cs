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
        public float3 LocalTargetOffset;
        public float ChaseTargetBlend;
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
            LocalTargetOffset = math.lerp(a.LocalTargetOffset, b.LocalTargetOffset, s),
            ChaseTargetBlend = math.lerp(a.ChaseTargetBlend, b.ChaseTargetBlend, s)
        };

        public PhysicsLinearPIDData Add(in PhysicsLinearPIDData a, in PhysicsLinearPIDData b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Integral = a.Integral + b.Integral,
            Derivative = a.Derivative + b.Derivative,
            MaxForce = a.MaxForce + b.MaxForce,
            LocalTargetOffset = a.LocalTargetOffset + b.LocalTargetOffset,
            ChaseTargetBlend = a.ChaseTargetBlend + b.ChaseTargetBlend
        };
    }
}