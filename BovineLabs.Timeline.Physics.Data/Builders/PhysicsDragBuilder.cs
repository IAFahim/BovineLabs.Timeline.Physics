using BovineLabs.Core.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Data;

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
