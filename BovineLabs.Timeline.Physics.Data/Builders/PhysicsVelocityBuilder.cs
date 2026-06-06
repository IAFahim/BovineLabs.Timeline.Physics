using BovineLabs.Core.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Data;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsVelocityBuilder
    {
        public PhysicsVelocityData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsVelocityAnimated { AuthoredData = AuthoredData });
        }
    }
}
