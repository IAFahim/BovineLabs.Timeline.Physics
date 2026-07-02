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

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. Lets the
        // mixer tell an authored "stop" clip (SetContinuous, zero velocity) apart from an empty slot — otherwise
        // a hard-brake clip is byte-identical to default(PhysicsVelocityData) and silently loses every crossfade.
        public byte Present;
    }

    public struct PhysicsVelocityState : IComponentData
    {
        public bool Fired;
        public bool ResetApplied;

        // Render-rate clip-active time (accumulated by PhysicsVelocityTrackSystem) and the amount already consumed
        // by the fixed-step AddContinuous integrator. Delivering velocity against (ElapsedTime - AppliedTime)
        // instead of the fixed dt makes total Δv framerate-independent. Mirrors PhysicsForceState.
        public float ElapsedTime;
        public float AppliedTime;
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