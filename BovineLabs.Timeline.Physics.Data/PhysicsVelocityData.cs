using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics.Data
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

        public VelocityResetFlags ResetVelocityOnFire;

        public StatStrengthConfig Strength;
    }

    public struct PhysicsVelocityState : IComponentData
    {
        public bool Fired;
        public bool ResetApplied;
    }

    public struct PhysicsVelocityAnimated : IAnimatedComponent<PhysicsVelocityData>, IPreparable
    {
        public PhysicsVelocityData AuthoredData;
        [CreateProperty] public PhysicsVelocityData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveVelocity : IActive<PhysicsVelocityData>
    {
        public PhysicsVelocityData Config { get; set; }
    }
}