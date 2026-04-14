using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsVelocityComponent : IComponentData
    {
        public PhysicsVelocity PhysicsVelocity;
        public bool IsLocalSpace;
        public Target Target;
    }
}