using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsTriggerConditionBuilder
    {
        public PhysicsTriggerConditionData ConditionData;
        public PhysicsTriggerFilterData FilterData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(ConditionData);
            builder.AddComponent(FilterData);
        }
    }
}