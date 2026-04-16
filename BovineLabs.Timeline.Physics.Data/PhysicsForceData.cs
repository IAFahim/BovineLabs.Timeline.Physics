using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsForceData
    {
        public float3 Linear;
        public float3 Angular;
    }

    public struct PhysicsForceAnimated : IAnimatedComponent<PhysicsForceData>
    {
        public PhysicsForceData AuthoredData;
        [CreateProperty] public PhysicsForceData Value { get; set; }
    }

    public readonly struct PhysicsForceMixer : IMixer<PhysicsForceData>
    {
        public PhysicsForceData Lerp(in PhysicsForceData a, in PhysicsForceData b, in float s) => new PhysicsForceData
        {
            Linear = math.lerp(a.Linear, b.Linear, s),
            Angular = math.lerp(a.Angular, b.Angular, s)
        };

        public PhysicsForceData Add(in PhysicsForceData a, in PhysicsForceData b) => new PhysicsForceData
        {
            Linear = a.Linear + b.Linear,
            Angular = a.Angular + b.Angular
        };
    }
}