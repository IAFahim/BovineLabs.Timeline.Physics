using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsKinematicOverrideData : IComponentData
    {
        public bool IsKinematic;
        public bool ZeroVelocityOnEnter;
        public bool ZeroGravity;
    }

    public struct PhysicsKinematicOverrideAnimated : IAnimatedComponent<PhysicsKinematicOverrideData>
    {
        public PhysicsKinematicOverrideData AuthoredData;
        [CreateProperty] public PhysicsKinematicOverrideData Value { get; set; }
    }

    public struct ActiveKinematicOverride : IComponentData, IEnableableComponent
    {
        public PhysicsKinematicOverrideData Config;
    }

    public struct PhysicsKinematicOverrideState : IComponentData
    {
        public bool Fired;
        public float OriginalGravityScale;
        public bool GravityCaptured;
        public bool AddedGravityComponent;
        public bool AddedMassOverrideComponent;
        public byte OriginalIsKinematic; // PhysicsMassOverride uses byte for IsKinematic
    }
}