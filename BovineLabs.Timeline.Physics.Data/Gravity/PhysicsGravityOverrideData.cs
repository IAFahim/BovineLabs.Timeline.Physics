using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsGravityOverrideData : IComponentData
    {
        public float GravityScale;
        public bool RestoreOnExit;

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. The neutral
        // for a gravity SCALE is 1 (normal gravity), not the default 0 (zero-G); the mixer keys off this flag to
        // blend an empty slot toward 1 instead of dragging the scale toward 0 at partial weight.
        public byte Present;
    }

    public struct PhysicsGravityOverrideAnimated : IAnimatedComponent<PhysicsGravityOverrideData>, IPreparable
    {
        public PhysicsGravityOverrideData AuthoredData;
        [CreateProperty] public PhysicsGravityOverrideData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveGravityOverride : IActive<PhysicsGravityOverrideData>
    {
        public PhysicsGravityOverrideData Config { get; set; }
    }

    public struct PhysicsGravityOverrideState : IComponentData, IRestorableState
    {
        public bool Fired;
        public float OriginalGravityScale;
        public bool AddedComponent;

        public bool RestorePending => this.Fired;
    }

    public struct PhysicsGravityOverrideMixer : IMixer<PhysicsGravityOverrideData>
    {
        public PhysicsGravityOverrideData Lerp(in PhysicsGravityOverrideData a, in PhysicsGravityOverrideData b,
            in float s)
        {
            // An empty slot means "no override" — neutral gravity, scale 1 — not zero-G (scale 0). Blend an empty
            // slot's scale toward 1 so the effect vanishes toward normal gravity at partial weight, and take the
            // discrete RestoreOnExit from the present side.
            var aDefault = a.Present == 0;
            var bDefault = b.Present == 0;

            var aScale = aDefault ? 1f : a.GravityScale;
            var bScale = bDefault ? 1f : b.GravityScale;
            var restore = bDefault || (!aDefault && s < 0.5f) ? a.RestoreOnExit : b.RestoreOnExit;

            return new PhysicsGravityOverrideData
            {
                GravityScale = math.lerp(aScale, bScale, s),
                RestoreOnExit = restore,
                Present = (byte)(a.Present | b.Present)
            };
        }

        public PhysicsGravityOverrideData Add(in PhysicsGravityOverrideData a, in PhysicsGravityOverrideData b)
        {
            return b.Present != 0 ? b : a;
        }
    }
}