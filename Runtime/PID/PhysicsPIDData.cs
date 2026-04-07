using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsPIDData
    {
        public float3 Proportional;
        public float3 Integral;
        public float3 Derivative;
        public float3 LocalTargetOffset; 
        
        // 0.0 = Chase our own LocalTargetOffset. 1.0 = Chase Reaction Target's location.
        public float ChaseTargetBlend; 
        public float MaxForce;
    }

    public struct PhysicsPIDAnimated : IAnimatedComponent<PhysicsPIDData>
    {
        public PhysicsPIDData AuthoredData;
        [CreateProperty] public PhysicsPIDData Value { get; set; }
    }

    public struct PhysicsPIDState : IComponentData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public bool IsInitialized;
    }
}