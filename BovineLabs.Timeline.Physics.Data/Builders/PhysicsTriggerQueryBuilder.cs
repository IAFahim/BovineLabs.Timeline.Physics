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

            // Wave 2: AllSurvivorsFanout / TopK may emit a capped hit buffer on the routed entity.
            // The query writes into the ROUTED entity's buffer; for Self-routing the clip itself needs it.
            if (QueryData.WriteHitBuffer)
                builder.AddBuffer<TriggerQueryHit>();
        }
    }
}