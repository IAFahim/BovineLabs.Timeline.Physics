using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Properties;

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
        [CreateProperty] public PhysicsFilterOverrideData Value { get; set; }
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
}