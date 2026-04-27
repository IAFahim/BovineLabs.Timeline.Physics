using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsVelocityMode : byte
    {
        SetContinuous,
        SetInstant,
        AddContinuous,
        AddInstant
    }

    public struct PhysicsVelocityData
    {
        public PhysicsVelocityMode Mode;
        public float3 Linear;
        public float3 Angular;
        public Target Space;
    }

    public struct PhysicsVelocityState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsVelocityAnimated : IAnimatedComponent<PhysicsVelocityData>
    {
        public PhysicsVelocityData AuthoredData;
        [CreateProperty] public PhysicsVelocityData Value { get; set; }
    }

    public struct ActiveVelocity : IComponentData, IEnableableComponent
    {
        public PhysicsVelocityData Config;
    }
}
