using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsBreakForceBuilder
    {
        public PhysicsBreakForceData BreakData;
        public PhysicsTriggerFilterData FilterData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(BreakData);
            builder.AddComponent(FilterData);
        }
    }
}
