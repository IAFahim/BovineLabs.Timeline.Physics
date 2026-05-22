using BovineLabs.Core;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    public readonly partial struct PhysicsBodyFacet : IFacet
    {
        public readonly RefRW<PhysicsVelocity> Velocity;

        public partial struct Lookup
        {
        }

        public partial struct TypeHandle
        {
        }
    }
}