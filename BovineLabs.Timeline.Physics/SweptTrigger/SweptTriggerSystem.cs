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
    /// <para>
    /// Swept events are ENTITY-granular, not collider-key granular: each contacted rigid body yields exactly
    /// one <c>StatefulTriggerEvent</c> (ColliderKeyB/BodyIndex left 0), whereas the simulation trigger emits
    /// one event per overlapping child collider of a compound target. Every consumer here reads only EntityB +
    /// State (never the collider key / body index), so Instantiate and all FirstPerRoot consumers behave
    /// identically; only HitMode=AllContacts on a compound target differs (swept applies once per entity).
    /// A swept source therefore must NOT also be a real Unity.Physics trigger body — this system owns and
    /// rewrites the buffer each frame and would clobber the simulation's events.
    /// </para>
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
        private ComponentLookup<SweptTriggerConfig> _sweptConfigLookup;
        private EntityQuery _activeClipQuery;

        /// <inheritdoc/>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SweptTriggerConfig>();
            this._sweptConfigLookup = state.GetComponentLookup<SweptTriggerConfig>(true);

            // Same entity set CollectActiveSourcesJob iterates; its count is the upper bound on distinct
            // active sources, so it sizes activeSources's fixed ParallelWriter capacity (F15).
            this._activeClipQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<ClipActive, TrackBinding>()
                .Build(ref state);
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
            // A NativeParallelHashSet written through a ParallelWriter does NOT grow, so size it to the actual
            // upper bound (one source per active clip) — a fixed cap would ThrowFull / silently drop sources at
            // scale (F15). math.max(1, ..) keeps capacity valid when no clips are active.
            var capacity = math.max(1, this._activeClipQuery.CalculateEntityCount());
            var activeSources = new NativeParallelHashSet<Entity>(capacity, state.WorldUpdateAllocator);

            this._sweptConfigLookup.Update(ref state);

            var collectHandle = new CollectActiveSourcesJob
            {
                ActiveSources = activeSources.AsParallelWriter(),
                SweptConfig = this._sweptConfigLookup,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new SweepJob
            {
                CollisionWorld = physicsWorld.PhysicsWorld.CollisionWorld,
                ActiveSources = activeSources,
                StorageInfo = SystemAPI.GetEntityStorageInfoLookup(),
            }.ScheduleParallel(collectHandle);
        }

        /// <summary>
        /// Collects the bound source of every active clip whose target is a swept source. Guarding on
        /// <see cref="SweptTriggerConfig"/> means only genuine swept sources are ever marked active — a clip
        /// of any other kind that happens to bind a non-swept entity can never spuriously arm a sweep.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct CollectActiveSourcesJob : IJobEntity
        {
            public NativeParallelHashSet<Entity>.ParallelWriter ActiveSources;

            [ReadOnly]
            public ComponentLookup<SweptTriggerConfig> SweptConfig;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value != Entity.Null && this.SweptConfig.HasComponent(binding.Value))
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

            [ReadOnly]
            public EntityStorageInfoLookup StorageInfo;

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

                    // ColliderCast translates the collider with a FIXED orientation, so a fast ROTATION between
                    // frames (the dominant melee case: a blade swinging about a near-stationary pivot) leaves an
                    // angular gap the linear cast cannot see — a thin target sitting between the start and end
                    // orientations would be missed. Auto-derive enough sub-steps that the blade TIP never arcs
                    // more than one capsule-radius of chord per sub-step, then honour the authored minimum. For
                    // slow / non-rotating swings the angle is ~0 so this collapses back to config.SubSteps at no
                    // extra cost; capped so a pathological spin can't explode the cast count.
                    var cosHalf = math.min(1f, math.abs(math.dot(startRot.value, curRot.value)));
                    var sweepAngle = 2f * math.acos(cosHalf);
                    var tipRadius = math.max(math.length(config.Vertex0), math.length(config.Vertex1));
                    var autoSteps = (int)math.ceil((tipRadius * sweepAngle) / math.max(config.Radius, 1e-4f));
                    var steps = math.clamp(math.max(config.SubSteps, autoSteps), 1, 32);

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

                    // Skip entities destroyed since last frame — emitting an Exit for a recycled/dead entity
                    // could reference a different (reused) entity. Consumers also guard, but don't emit it.
                    if (!Contains(in current, e) && this.StorageInfo.Exists(e))
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
