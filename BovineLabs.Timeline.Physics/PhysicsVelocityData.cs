using BovineLabs.Timeline.Data;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsVelocityData
    {
        public float3 Linear;
        public float3 Angular;
    }

    public struct PhysicsVelocityAnimated : IAnimatedComponent<PhysicsVelocityData>
    {
        public PhysicsVelocityData AuthoredVelocity;
        public bool IsLocalSpace;

        [CreateProperty] public PhysicsVelocityData Value { get; set; }
    }
}