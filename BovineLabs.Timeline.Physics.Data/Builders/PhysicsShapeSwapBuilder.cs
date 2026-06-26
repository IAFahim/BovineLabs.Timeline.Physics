using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsShapeSwapBuilder
    {
        public PhysicsShapeSwapData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsShapeSwapAnimated { AuthoredData = AuthoredData });
        }
    }
}
