namespace BovineLabs.Timeline.Physics.Data
{
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Timeline.Data;
    using Data.Kernel;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Properties;

    public enum PhysicsVelocityMode : byte
    {
        /// <summary>
        /// Continuously overrides the velocity. Note this is applied post-integration (effective next frame).
        /// </summary>
        SetContinuous,

        /// <summary>
        /// Instantly overrides the velocity. Note this is applied post-integration (effective next frame).
        /// </summary>
        SetInstant,

        /// <summary>
        /// Continuously applies a velocity change pre-integration.
        /// </summary>
        AddContinuous,

        /// <summary>
        /// Instantly applies a velocity change pre-integration.
        /// </summary>
        AddInstant
    }

    public struct PhysicsVelocityData
    {
        public PhysicsVelocityMode Mode;
        public float3 Linear;
        public float3 Angular;
        public Target Space;
        public StatStrengthConfig Strength;
    }

    public struct PhysicsVelocityState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsVelocityAnimated : IAnimatedComponent<PhysicsVelocityData>, IPreparable
    {
        public PhysicsVelocityData AuthoredData;
        [CreateProperty] public PhysicsVelocityData Value { get; set; }

        public void ResetToAuthored() => Value = AuthoredData;
    }

    public struct ActiveVelocity : IActive<PhysicsVelocityData>
    {
        public PhysicsVelocityData Config { get; set; }
    }
}