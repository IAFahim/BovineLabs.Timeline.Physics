using BovineLabs.Timeline.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsFilterOverrideData : IComponentData
    {
        public uint BelongsToOverride;
        public uint CollidesWithOverride;
        public bool RestoreOnExit;
    }

    public struct PhysicsFilterOverrideAnimated : IAnimatedComponent<PhysicsFilterOverrideData>
    {
        public PhysicsFilterOverrideData AuthoredData;
        public PhysicsFilterOverrideData Value { get; set; }
        

    }

    public struct ActiveFilterOverride : IComponentData, IEnableableComponent
    {
        public PhysicsFilterOverrideData Config;
    }

    public struct PhysicsFilterOverrideState : IComponentData
    {
        public bool Fired;
        public uint OriginalBelongsTo;
        public uint OriginalCollidesWith;
    }

    public struct PhysicsFilterOverrideMixer : IMixer<PhysicsFilterOverrideData>
    {
        public PhysicsFilterOverrideData Lerp(in PhysicsFilterOverrideData a, in PhysicsFilterOverrideData b, in float s)
        {
            return s >= 0.5f ? b : a;
        }

        public PhysicsFilterOverrideData Add(in PhysicsFilterOverrideData a, in PhysicsFilterOverrideData b)
        {
            return b;
        }
    }
}
