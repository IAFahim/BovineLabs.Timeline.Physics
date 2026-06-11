using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsTriggerQueryBuilder
    {
        public PhysicsTriggerQueryData QueryData;
        public PhysicsTriggerFilterData FilterData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(QueryData);
            builder.AddComponent(FilterData);
            builder.AddComponent(default(PhysicsTriggerQueryState));
        }
    }
}
