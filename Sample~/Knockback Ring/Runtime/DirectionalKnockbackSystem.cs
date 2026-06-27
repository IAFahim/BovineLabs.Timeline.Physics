namespace Vex.Knockback
{
    using BovineLabs.Core.PhysicsStates;
    using BovineLabs.Timeline.Physics;                 // PendingExternalForce
    using BovineLabs.Timeline.Physics.Forces;          // PhysicsProducerForceAccumulatorSystem
    using BovineLabs.Timeline.Physics.Infrastructure;  // PhysicsProducerGroup
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    /// <summary>
    /// Reads each <see cref="KnockbackReceiver"/> body's <c>StatefulTriggerEvent</c> buffer and, on every trigger
    /// <c>Enter</c>, appends a one-shot <c>PendingExternalForce</c> impulse directed away (on the XZ plane) from the
    /// entering body, plus a vertical leap. The compose system drains it into the standing external-velocity channel
    /// later in the same fixed step, so each touch produces one clean knockback that braking/drag can't eat.
    /// </summary>
    /// <remarks>
    /// Placed in <see cref="PhysicsProducerGroup"/> before <see cref="PhysicsProducerForceAccumulatorSystem"/> — the
    /// same seam the engine's own trigger-force consumer uses — so the stateful trigger events have already been
    /// produced upstream and the impulse is consumed downstream the same step (mirrors PhysicsTriggerForceSystem).
    /// </remarks>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct DirectionalKnockbackSystem : ISystem
    {
        private ComponentLookup<LocalToWorld> _ltwLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ltwLookup = state.GetComponentLookup<LocalToWorld>(true);
            state.RequireForUpdate<KnockbackReceiver>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ltwLookup.Update(ref state);

            state.Dependency = new KnockbackJob
            {
                LtwLookup = _ltwLookup,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct KnockbackJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;

            private void Execute(
                in KnockbackReceiver receiver,
                in LocalToWorld ltw,
                ref DynamicBuffer<StatefulTriggerEvent> events,
                ref DynamicBuffer<PendingExternalForce> pending)
            {
                if (events.Length == 0)
                {
                    return;
                }

                var selfPos = ltw.Position;

                // One knockback per entering body: a compound of overlapping sphere leaves can raise several Enter
                // events for the same body in a frame, and we don't want to multiply the impulse.
                var handled = new FixedList128Bytes<Entity>();

                for (var i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (evt.State != StatefulEventState.Enter)
                    {
                        continue;
                    }

                    if (!LtwLookup.HasComponent(evt.EntityB) || handled.Contains(evt.EntityB))
                    {
                        continue;
                    }

                    if (handled.Length < handled.Capacity)
                    {
                        handled.Add(evt.EntityB);
                    }

                    var away = selfPos - LtwLookup[evt.EntityB].Position;
                    away.y = 0f;

                    var horizontal = math.lengthsq(away) > 1e-6f
                        ? math.normalize(away) * receiver.Strength
                        : float3.zero;

                    pending.Add(new PendingExternalForce
                    {
                        Linear = new float3(horizontal.x, receiver.Lift, horizontal.z),
                        Angular = float3.zero,
                    });
                }
            }
        }
    }
}
