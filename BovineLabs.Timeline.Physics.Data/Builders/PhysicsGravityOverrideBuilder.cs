using BovineLabs.Core.EntityCommands;
using BovineLabs.Timeline.Physics;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsGravityOverrideBuilder
    {
        public PhysicsGravityOverrideData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsGravityOverrideAnimated { AuthoredData = AuthoredData });
        }
    }
}
