namespace BovineLabs.Timeline.Physics.SweptTrigger
{
    using BovineLabs.Core.PhysicsStates;
    using BovineLabs.Timeline.Data;
    using BovineLabs.Timeline.Physics.Infrastructure;
    using BovineLabs.Timeline.Physics.TriggerEvents;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;
    using Unity.Transforms;

    /// <summary>
    /// The SWEPT companion to the simulation-driven stateful trigger. For each source carrying a
    /// <see cref="SweptTriggerConfig"/> that has an ACTIVE clip this frame, it sweeps the dummy collider
    /// from the source's previous to current world transform, diffs the overlapped set against last frame,
    /// and writes Enter / Stay / Exit into the source's <c>StatefulTriggerEvent</c> buffer — the very buffer
    /// the simulation fills for a real trigger. It therefore runs in <see cref="PhysicsProducerGroup"/>
    /// BEFORE every consumer system, so the existing Instantiate / Force / Condition / BreakForce / Query
    /// clips consume swept events identically, with zero changes to them or to core. Core's clear/stateful
    /// systems run after simulation and never collide with these writes.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsTriggerQuerySystem))]
    [UpdateBefore(typeof(PhysicsTriggerInstantiateSystem))]
    [UpdateBefore(typeof(PhysicsTriggerForceSystem))]
    [UpdateBefore(typeof(PhysicsTriggerConditionSystem))]
    [UpdateBefore(typeof(PhysicsBreakForceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct SweptTriggerSystem : ISystem
    {
        /// <inheritdoc/>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SweptTriggerConfig>();
        }

        /// <inheritdoc/>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld))
            {
                return;
            }

            // Sources that have at least one active clip this frame (the "active at precise moments" gate).
            var activeSources = new NativeParallelHashSet<Entity>(128, state.WorldUpdateAllocator);

            var collectHandle = new CollectActiveSourcesJob
            {
                ActiveSources = activeSources.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new SweepJob
            {
                CollisionWorld = physicsWorld.PhysicsWorld.CollisionWorld,
                ActiveSources = activeSources,
            }.ScheduleParallel(collectHandle);
        }

        /// <summary>Collects the bound source of every active clip (any track) into the active set.</summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct CollectActiveSourcesJob : IJobEntity
        {
            public NativeParallelHashSet<Entity>.ParallelWriter ActiveSources;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value != Entity.Null)
                {
                    this.ActiveSources.Add(binding.Value);
                }
            }
        }

        /// <summary>Sweeps each swept source and writes Enter/Stay/Exit edges into its trigger buffer.</summary>
        [BurstCompile]
        private partial struct SweepJob : IJobEntity
        {
            [ReadOnly]
            public CollisionWorld CollisionWorld;

            [ReadOnly]
            public NativeParallelHashSet<Entity> ActiveSources;

            private void Execute(
                Entity entity,
                ref DynamicBuffer<StatefulTriggerEvent> events,
                ref DynamicBuffer<SweptTriggerHit> prevHits,
                ref SweptTriggerState state,
                in SweptTriggerConfig config,
                in LocalToWorld ltw)
            {
                var active = this.ActiveSources.Contains(entity);
                var curPos = ltw.Position;
                var curRot = ltw.Rotation;

                var current = new NativeList<Entity>(8, Allocator.Temp);

                if (active && config.Collider.IsCreated)
                {
                    // (1) CURRENT-OVERLAP pass. A zero/near-zero ColliderCast does not report a body the volume is
                    // already penetrating, so a resting / slow / first-activation overlap would be missed. A
                    // distance query at the current pose catches those (MaxDistance 0 = touching/penetrating).
                    var dist = new NativeList<DistanceHit>(16, Allocator.Temp);
                    var dInput = new ColliderDistanceInput(config.Collider, 0f, new RigidTransform(curRot, curPos));
                    if (this.CollisionWorld.CalculateDistance(dInput, ref dist))
                    {
                        for (var h = 0; h < dist.Length; h++)
                        {
                            Accumulate(ref current, dist[h].Entity, entity);
                        }
                    }

                    dist.Dispose();

                    // (2) SWEPT pass prev->cur. ColliderCast translates the collider with a FIXED orientation (it
                    // cannot rotate it mid-cast), so for a rotating blade we sub-step and orient each sub-segment
                    // at its own midpoint. More sub-steps = finer rotation coverage for very fast swings.
                    var startPos = state.WasActive == 1 ? state.PrevPosition : curPos;
                    var startRot = state.WasActive == 1 ? state.PrevRotation : curRot;
                    var steps = math.max(1, config.SubSteps);

                    var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);
                    for (var s = 0; s < steps; s++)
                    {
                        var t0 = (float)s / steps;
                        var t1 = (float)(s + 1) / steps;
                        var p0 = math.lerp(startPos, curPos, t0);
                        var p1 = math.lerp(startPos, curPos, t1);
                        var rot = math.slerp(startRot, curRot, (t0 + t1) * 0.5f);

                        castHits.Clear();
                        var input = new ColliderCastInput(config.Collider, p0, p1, rot);
                        if (this.CollisionWorld.CastCollider(input, ref castHits))
                        {
                            for (var h = 0; h < castHits.Length; h++)
                            {
                                Accumulate(ref current, castHits[h].Entity, entity);
                            }
                        }
                    }

                    castHits.Dispose();
                }

                events.Clear();

                // Enter (new) + Stay (still overlapping).
                for (var i = 0; i < current.Length; i++)
                {
                    var e = current[i];
                    var stay = ContainsBuffer(in prevHits, e);
                    events.Add(new StatefulTriggerEvent
                    {
                        EntityB = e,
                        State = stay ? StatefulEventState.Stay : StatefulEventState.Enter,
                    });
                }

                // Exit (overlapped last frame, gone now).
                for (var i = 0; i < prevHits.Length; i++)
                {
                    var e = prevHits[i].Value;
                    if (!Contains(in current, e))
                    {
                        events.Add(new StatefulTriggerEvent
                        {
                            EntityB = e,
                            State = StatefulEventState.Exit,
                        });
                    }
                }

                prevHits.Clear();
                for (var i = 0; i < current.Length; i++)
                {
                    prevHits.Add(new SweptTriggerHit { Value = current[i] });
                }

                state.PrevPosition = curPos;
                state.PrevRotation = curRot;
                state.WasActive = (byte)(active ? 1 : 0);

                current.Dispose();
            }

            private static void Accumulate(ref NativeList<Entity> list, Entity e, Entity self)
            {
                if (e == Entity.Null || e == self)
                {
                    return;
                }

                if (!Contains(in list, e))
                {
                    list.Add(e);
                }
            }

            private static bool Contains(in NativeList<Entity> list, Entity e)
            {
                for (var i = 0; i < list.Length; i++)
                {
                    if (list[i] == e)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool ContainsBuffer(in DynamicBuffer<SweptTriggerHit> buffer, Entity e)
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Value == e)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
