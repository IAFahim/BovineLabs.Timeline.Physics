using BovineLabs.Core.EntityCommands;
using BovineLabs.Timeline.Physics;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsFilterOverrideBuilder
    {
        public PhysicsFilterOverrideData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsFilterOverrideAnimated { AuthoredData = AuthoredData });
        }
    }
}
