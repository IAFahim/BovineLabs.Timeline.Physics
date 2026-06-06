using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsLinearPIDBuilder
    {
        public PhysicsLinearPIDData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsLinearPIDAnimated { AuthoredData = AuthoredData });
        }
    }
}