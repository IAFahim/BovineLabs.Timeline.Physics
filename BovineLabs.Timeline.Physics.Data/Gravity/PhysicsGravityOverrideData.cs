using BovineLabs.Timeline.Data;
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

    public struct PhysicsGravityOverrideAnimated : IAnimatedComponent<PhysicsGravityOverrideData>
    {
        public PhysicsGravityOverrideData AuthoredData;
        [CreateProperty] public PhysicsGravityOverrideData Value { get; set; }
    }

    public struct ActiveGravityOverride : IComponentData, IEnableableComponent
    {
        public PhysicsGravityOverrideData Config;
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