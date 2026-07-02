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

        // 1 for any authored clip, 0 for the blend framework's empty-slot fill. Without it an authored all-zero
        // override ("belongs to nothing / collides with nothing" = phase through everything) is byte-identical to
        // default and DiscreteMixer discards it during a crossfade. Present makes the memcmp distinguish them.
        public byte Present;
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

        // Set once we've emitted the "shared collider, override skipped" diagnostic for this body, so the
        // warning fires once instead of every frame the clip is active.
        public bool WarnedShared;
    }
}