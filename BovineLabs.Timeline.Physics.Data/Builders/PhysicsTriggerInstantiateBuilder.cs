using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Data.Builders
{
    public struct PhysicsTriggerInstantiateBuilder
    {
        public PhysicsTriggerInstantiateData InstantiateData;
        public PhysicsTriggerFilterData FilterData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(InstantiateData);
            builder.AddComponent(FilterData);
            builder.AddComponent(default(PhysicsClipGate));
            builder.SetComponentEnabled<PhysicsClipGate>(false);
        }
    }
}