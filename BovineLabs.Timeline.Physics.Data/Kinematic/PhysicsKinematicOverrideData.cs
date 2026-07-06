using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
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

    public struct PhysicsKinematicOverrideAnimated : IAnimatedComponent<PhysicsKinematicOverrideData>, IPreparable
    {
        public PhysicsKinematicOverrideData AuthoredData;
        [CreateProperty] public PhysicsKinematicOverrideData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveKinematicOverride : IActive<PhysicsKinematicOverrideData>
    {
        public PhysicsKinematicOverrideData Config { get; set; }
    }

    public struct PhysicsKinematicOverrideState : IComponentData, IRestorableState
    {
        public bool Fired;
        public float OriginalGravityScale;
        public bool GravityCaptured;
        public bool AddedGravityComponent;
        public bool AddedMassOverrideComponent;
        public byte OriginalIsKinematic;

        public bool RestorePending => this.Fired;
    }
}