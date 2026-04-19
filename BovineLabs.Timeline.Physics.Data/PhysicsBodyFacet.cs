using BovineLabs.Core;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public readonly partial struct PhysicsBodyFacet : IFacet
    {
        public readonly RefRW<PhysicsVelocity> Velocity;
        public readonly RefRO<LocalTransform> Transform;
        [FacetOptional] public readonly RefRO<PhysicsMass> Mass;

        public partial struct Lookup
        {
        }

        public partial struct TypeHandle
        {
        }
    }
}