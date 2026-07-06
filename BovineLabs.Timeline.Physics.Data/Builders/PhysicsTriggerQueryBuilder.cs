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
            builder.AddComponent(default(PhysicsClipGate));
            builder.SetComponentEnabled<PhysicsClipGate>(false);

            // Wave 2: AllSurvivorsFanout / TopK may emit a capped TriggerQueryHit buffer. It belongs on the ROUTED
            // entity, which is resolved at runtime (Self/Owner/Source/Target/links) and is generally NOT this clip —
            // so it can't be baked here. PhysicsTriggerQuerySystem.ApplyJob adds it to the routed entity on demand.
        }
    }
}