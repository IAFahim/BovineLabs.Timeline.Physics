using BovineLabs.Core.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsRicochetBuilder
    {
        public PhysicsRicochetData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsRicochetAnimated { AuthoredData = AuthoredData });
        }
    }
}
