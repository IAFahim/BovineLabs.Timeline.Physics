using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsSplineFollowBuilder
    {
        public PhysicsSplineFollowData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsSplineFollowAnimated { AuthoredData = AuthoredData });
        }
    }
}
