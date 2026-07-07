using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
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

        public StatSource Strength;

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. Lets the
        // mixer tell an authored "stop" clip (SetContinuous, zero velocity) apart from an empty slot — otherwise
        // a hard-brake clip is byte-identical to default(PhysicsVelocityData) and silently loses every crossfade.
        public byte Present;
    }

    public struct PhysicsVelocityState : IComponentData, IElapsedTimeState, IDrainableLatchState<PhysicsVelocityData>
    {
        public bool Fired;
        public bool ResetApplied;

        // Render-rate clip-active time (accumulated by PhysicsVelocityTrackSystem) and the amount already consumed
        // by the fixed-step AddContinuous integrator. Delivering velocity against (ElapsedTime - AppliedTime)
        // instead of the fixed dt makes total Δv framerate-independent. Mirrors PhysicsForceState.
        // As with force, the tail remainder a fixed-step-less clip-end used to discard is now drained by the
        // fixed-step linger (see Orphaned) — the total Δv is exact; only its per-step placement stays bounded-phase-
        // dependent (≤ ~1 fixed step).
        public float ElapsedTime;
        public float AppliedTime;

        /// <summary>
        /// Fixed-step drain gate: set when the render-side stale-disable lingers this latch enabled because it still
        /// owes fixed-step work (an unfired instant add/set, or an unconsumed continuous tail), so the fixed clock
        /// gets one apply tick to service it before the drain-finalize disables it. Mirrors PhysicsForceState.
        /// </summary>
        public bool Orphaned;

        float IElapsedTimeState.ElapsedTime
        {
            get => ElapsedTime;
            set => ElapsedTime = value;
        }

        bool IOrphanedLatch.Orphaned
        {
            get => Orphaned;
            set => Orphaned = value;
        }

        public bool IsDrained(in PhysicsVelocityData config)
        {
            switch (config.Mode)
            {
                // Instant add (producer) and instant set (modifier) are one-shot: done once fired.
                case PhysicsVelocityMode.AddInstant:
                case PhysicsVelocityMode.SetInstant:
                    return Fired;

                // Continuous add integrates the clip-active tail; done once AppliedTime catches ElapsedTime.
                case PhysicsVelocityMode.AddContinuous:
                    return AppliedTime + 1e-6f >= ElapsedTime;

                // Continuous set is an idempotent per-step stomp with no accumulated tail to drain — disable at once.
                default:
                    return true;
            }
        }
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