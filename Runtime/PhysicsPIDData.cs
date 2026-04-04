using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    // The raw data that will be blended
    public struct PhysicsPIDData
    {
        public float3 Proportional;
        public float3 Integral;
        public float3 Derivative;
        public float3 Offset;
        public float MaxForce;
    }

    // The animated component attached to the clip
    public struct PhysicsPIDAnimated : IAnimatedComponent<PhysicsPIDData>
    {
        public Entity ExplicitTarget;
        public bool UseReactionTargets;
        public bool IsLocalOffset;
        
        public PhysicsPIDData AuthoredData;

        // Required by Timeline blending core
        [CreateProperty] public PhysicsPIDData Value { get; set; }
    }

    // The memory state attached to the missile/body being moved
    public struct PhysicsPIDState : IComponentData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public bool IsInitialized;
    }
}