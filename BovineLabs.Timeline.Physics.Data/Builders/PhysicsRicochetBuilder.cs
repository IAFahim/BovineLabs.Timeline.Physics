using BovineLabs.Core.EntityCommands;

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