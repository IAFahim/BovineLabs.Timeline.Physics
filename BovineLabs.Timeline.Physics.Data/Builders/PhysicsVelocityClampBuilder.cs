using BovineLabs.Core.EntityCommands;
using BovineLabs.Timeline.Physics;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsVelocityClampBuilder
    {
        public PhysicsVelocityClampData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsVelocityClampAnimated { AuthoredData = AuthoredData });
        }
    }
}
