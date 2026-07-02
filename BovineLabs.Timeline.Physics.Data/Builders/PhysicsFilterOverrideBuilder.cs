using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsFilterOverrideBuilder
    {
        public PhysicsFilterOverrideData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            var authored = AuthoredData;
            authored.Present = 1; // mark as a real authored clip so DiscreteMixer never treats it as an empty slot
            builder.AddComponent(new PhysicsFilterOverrideAnimated { AuthoredData = authored });
        }
    }
}