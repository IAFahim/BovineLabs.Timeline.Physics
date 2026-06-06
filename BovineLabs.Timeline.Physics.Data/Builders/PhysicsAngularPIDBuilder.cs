using BovineLabs.Core.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsAngularPIDBuilder
    {
        public PhysicsAngularPIDData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsAngularPIDAnimated { AuthoredData = AuthoredData });
        }
    }
}
