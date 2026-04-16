using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsVelocityData
    {
        public float3 Linear;
        public float3 Angular;
        public Target Space;
    }

    public struct PhysicsVelocityAnimated : IAnimatedComponent<PhysicsVelocityData>
    {
        public PhysicsVelocityData AuthoredVelocity;
        [CreateProperty] public PhysicsVelocityData Value { get; set; }
    }

    public struct ActiveVelocity : IComponentData, IEnableableComponent
    {
        public PhysicsVelocityData Config;
    }
}
