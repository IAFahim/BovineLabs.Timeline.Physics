using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsForceMode : byte
    {
        Continuous,
        Impulse
    }

    public struct PhysicsForceData
    {
        public PhysicsForceMode Mode;
        public float3 Linear;
        public float3 Angular;
        public Target Space;
    }

    public struct PhysicsForceState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsForceAnimated : IAnimatedComponent<PhysicsForceData>
    {
        public PhysicsForceData AuthoredData;
        [CreateProperty] public PhysicsForceData Value { get; set; }
    }

    public struct ActiveForce : IComponentData, IEnableableComponent
    {
        public PhysicsForceData Config;
    }

    public readonly struct PhysicsForceMixer : IMixer<PhysicsForceData>
    {
        public PhysicsForceData Lerp(in PhysicsForceData a, in PhysicsForceData b, in float s)
        {
            return new PhysicsForceData
            {
                Mode = s < 0.5f ? a.Mode : b.Mode,
                Linear = math.lerp(a.Linear, b.Linear, s),
                Angular = math.lerp(a.Angular, b.Angular, s),
                Space = s < 0.5f ? a.Space : b.Space
            };
        }

        public PhysicsForceData Add(in PhysicsForceData a, in PhysicsForceData b)
        {
            return new PhysicsForceData
            {
                Mode = a.Mode,
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = a.Space
            };
        }
    }
}
