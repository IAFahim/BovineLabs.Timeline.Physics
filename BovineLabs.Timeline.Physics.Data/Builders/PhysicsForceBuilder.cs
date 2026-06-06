using BovineLabs.Core.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Data;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsForceBuilder
    {
        public PhysicsForceData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsForceAnimated { AuthoredData = AuthoredData });
        }
    }
}
