namespace BovineLabs.Timeline.Physics.Bindings
{
    using BovineLabs.Timeline.Physics.Infrastructure;
    using BovineLabs.Timeline.Physics.TriggerEvents;
    using Unity.Burst;
    using Unity.Entities;

    /// <summary>
    /// Reference driver for the TauntOverride mechanism: converts an enabled <see cref="TriggerQueryTauntRequest"/>
    /// into a stamped <see cref="TriggerQueryTaunt.UntilTime"/>, then disables the request so it fires once per
    /// enable. The expiry is written as <c>SystemAPI.Time.ElapsedTime + Duration</c> read INSIDE
    /// <see cref="PhysicsProducerGroup"/> — the exact same clock <see cref="PhysicsTriggerQuerySystem"/> compares
    /// against (<c>taunt.UntilTime &gt; (float)SystemAPI.Time.ElapsedTime</c>). Both sit in the fixed-step group, so
    /// there is no render-vs-fixed clock mismatch. Updates before the query so a taunt requested this step is live
    /// this step.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsTriggerQuerySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct TriggerQueryTauntSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TriggerQueryTauntRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new TauntRequestJob
            {
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct TauntRequestJob : IJobEntity
        {
            public float ElapsedTime;

            // NOTE: `request` is taken by `ref` (not `in`) on purpose. Toggling the enabled bit via
            // EnabledRefRW<TriggerQueryTauntRequest> already forces a read-write ComponentTypeHandle for this
            // type; an `in` parameter would make IJobEntity emit a SECOND, read-only handle for the same type,
            // and the two aliasing RO/RW handles trip Unity's job safety system at schedule time. A single RW
            // handle (ref + EnabledRefRW) serves both the data read and the enabled toggle.
            private void Execute(ref TriggerQueryTaunt taunt, ref TriggerQueryTauntRequest request, EnabledRefRW<TriggerQueryTauntRequest> enabled)
            {
                taunt.UntilTime = ElapsedTime + request.Duration;
                enabled.ValueRW = false;
            }
        }
    }
}
