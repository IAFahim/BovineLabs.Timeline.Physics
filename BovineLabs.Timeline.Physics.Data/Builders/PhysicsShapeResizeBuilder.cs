using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsShapeResizeBuilder
    {
        public PhysicsShapeResizeData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsShapeResizeAnimated { AuthoredData = AuthoredData });
        }
    }
}
