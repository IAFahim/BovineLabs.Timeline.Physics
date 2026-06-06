using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsKinematicOverrideBuilder
    {
        public PhysicsKinematicOverrideData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsKinematicOverrideAnimated { AuthoredData = AuthoredData });
        }
    }
}