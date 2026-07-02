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

    public struct PhysicsGravityOverrideState : IComponentData
    {
        public bool Fired;
        public float OriginalGravityScale;
        public bool AddedComponent;
    }

    public struct PhysicsGravityOverrideMixer : IMixer<PhysicsGravityOverrideData>
    {
        public PhysicsGravityOverrideData Lerp(in PhysicsGravityOverrideData a, in PhysicsGravityOverrideData b,
            in float s)
        {
            return new PhysicsGravityOverrideData
            {
                GravityScale = math.lerp(a.GravityScale, b.GravityScale, s),
                RestoreOnExit = s >= 0.5f ? b.RestoreOnExit : a.RestoreOnExit
            };
        }

        public PhysicsGravityOverrideData Add(in PhysicsGravityOverrideData a, in PhysicsGravityOverrideData b)
        {
            return b;
        }
    }
}