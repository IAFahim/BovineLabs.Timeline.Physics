using Unity.Mathematics;
using Unity.Properties;
using BovineLabs.Timeline.Data;

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

        // The processed value consumed by TrackBlendImpl
        [CreateProperty] public PhysicsVelocityData Value { get; set; }
    }
}