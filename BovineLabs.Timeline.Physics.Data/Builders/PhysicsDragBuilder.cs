using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsDragBuilder
    {
        public PhysicsDragData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new PhysicsDragAnimated { AuthoredData = AuthoredData });
        }
    }
}