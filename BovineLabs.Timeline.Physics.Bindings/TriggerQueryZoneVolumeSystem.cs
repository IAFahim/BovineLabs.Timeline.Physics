namespace BovineLabs.Timeline.Physics.Bindings
{
    using BovineLabs.Nerve.PhysicsStates;
    using BovineLabs.Timeline.Physics.Infrastructure;
    using BovineLabs.Timeline.Physics.TriggerEvents;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// Reference driver for the ZoneStateGate mechanism: enables the candidate body's <see cref="TriggerQueryZoneTag"/>
    /// while it currently overlaps any <see cref="TriggerQueryZoneVolume"/> trigger body, and disables it once it has
    /// left them all. Membership is read from the body's own <c>StatefulTriggerEvent</c> buffer — the same package
    /// stateful-trigger stream the Instantiate/Condition/Force trigger tracks consume — so no new physics query is
    /// introduced. Runs in <see cref="PhysicsProducerGroup"/> before <see cref="PhysicsTriggerQuerySystem"/> so the
    /// tag is current for the gate the same fixed step.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsTriggerQuerySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct TriggerQueryZoneVolumeSystem : ISystem
    {
        private ComponentLookup<TriggerQueryZoneVolume> _zoneVolumeLookup;
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _zoneVolumeLookup = state.GetComponentLookup<TriggerQueryZoneVolume>(true);

            // IgnoreComponentEnabledState so bodies whose tag is currently DISABLED (outside a zone) are still visited
            // and can be re-enabled the moment they enter one.
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<StatefulTriggerEvent>()
                .WithAllRW<TriggerQueryZoneTag>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build(ref state);

            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _zoneVolumeLookup.Update(ref state);

            state.Dependency = new ZoneMembershipJob
            {
                ZoneVolumeLookup = _zoneVolumeLookup,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct ZoneMembershipJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<TriggerQueryZoneVolume> ZoneVolumeLookup;

            private void Execute(in DynamicBuffer<StatefulTriggerEvent> events, EnabledRefRW<TriggerQueryZoneTag> zoneEnabled)
            {
                var inside = false;

                for (var i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (evt.State == StatefulEventState.Exit)
                    {
                        continue;
                    }

                    if (ZoneVolumeLookup.HasComponent(evt.EntityB))
                    {
                        inside = true;
                        break;
                    }
                }

                zoneEnabled.ValueRW = inside;
            }
        }
    }
}
