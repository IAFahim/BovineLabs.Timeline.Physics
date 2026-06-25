using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.Stats;
using BovineLabs.Timeline.Physics.Teleports;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.IntegerTime;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsKinematicsApplySystem))]
    [UpdateBefore(typeof(PhysicsTriggerConditionSystem))]
    [UpdateBefore(typeof(PhysicsTriggerForceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsTriggerQuerySystem : ISystem
    {
        /// <summary> Hard cap on per-candidate tracked state (DwellSelect / PerTargetRefractory). O(n^2) ok at cap. </summary>
        private const int MaxTrackedCandidates = 8;

        private UnsafeComponentLookup<Targets> _targetsReadLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
        private ComponentLookup<FactionMember> _factionLookup;
        private ComponentLookup<TriggerQueryZoneTag> _zoneTagLookup;
        private ComponentLookup<TriggerQueryExposure> _exposureLookup;
        private ComponentLookup<TriggerQueryTaunt> _tauntLookup;
        private BufferLookup<Stat> _statLookup;
        private ComponentLookup<Targets> _targetsWriteLookup;
        private BufferLookup<TriggerQueryHit> _hitLookup;
        private ConditionEventWriter.Lookup _writers;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsReadLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _factionLookup = state.GetComponentLookup<FactionMember>(true);
            _zoneTagLookup = state.GetComponentLookup<TriggerQueryZoneTag>(true);
            _exposureLookup = state.GetComponentLookup<TriggerQueryExposure>(true);
            _tauntLookup = state.GetComponentLookup<TriggerQueryTaunt>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _targetsWriteLookup = state.GetComponentLookup<Targets>();
            _hitLookup = state.GetBufferLookup<TriggerQueryHit>();
            _writers.Create(ref state);

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsTriggerQueryState>()
                .WithAll<TrackBinding, PhysicsTriggerQueryData, PhysicsTriggerFilterData, PhysicsClipGate>()
                .Build();

            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsReadLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _zoneTagLookup.Update(ref state);
            _exposureLookup.Update(ref state);
            _tauntLookup.Update(ref state);
            _statLookup.Update(ref state);
            _targetsWriteLookup.Update(ref state);
            _hitLookup.Update(ref state);
            _writers.Update(ref state);

            var hasCollisionWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld);

            var events = new NativeStream(_query.CalculateChunkCountWithoutFiltering(), state.WorldUpdateAllocator);

            state.Dependency = new GatherJob
            {
                Events = events.AsWriter(),
                TrackBindingTypeHandle = SystemAPI.GetComponentTypeHandle<TrackBinding>(true),
                QueryDataTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsTriggerQueryData>(true),
                FilterDataTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true),
                QueryStateTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsTriggerQueryState>(),
                PhysicsClipGateTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsClipGate>(true),
                TimerDataTypeHandle = SystemAPI.GetComponentTypeHandle<TimerData>(true),
                TimeTransformTypeHandle = SystemAPI.GetComponentTypeHandle<TimeTransform>(true),
                TargetsLookup = _targetsReadLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ColliderLookup = _colliderLookup,
                LtwLookup = _ltwLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                VelocityLookup = _velocityLookup,
                MassLookup = _massLookup,
                FactionLookup = _factionLookup,
                ZoneTagLookup = _zoneTagLookup,
                ExposureLookup = _exposureLookup,
                TauntLookup = _tauntLookup,
                StatLookup = _statLookup,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true),
                HasCollisionWorld = hasCollisionWorld,
                CollisionWorld = hasCollisionWorld ? physicsWorld.CollisionWorld : default,
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(_query, state.Dependency);

            state.Dependency = new ApplyJob
            {
                Events = events,
                TargetsLookup = _targetsWriteLookup,
                HitLookup = _hitLookup,
                Writers = _writers
            }.Schedule(state.Dependency);
        }

        private struct TriggerQueryEvent
        {
            public Entity Routed;
            public Entity Winner;
            public Entity Self; // for MirrorIntoWinner (single-threaded write); Null = no mirror
            public bool WriteSlot;
            public bool ClearHitBuffer; // first event for this query clears the hit buffer
            public bool WriteHit;
            public PhysicsTriggerRouteSlot Slot;
            public PhysicsTriggerWriteMode WriteMode;
            public ConditionKey Condition;
            public int Value;
            public int Sector;
            public int Band;
            public int Score;
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public NativeStream.Writer Events;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerQueryData> QueryDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> FilterDataTypeHandle;
            public ComponentTypeHandle<PhysicsTriggerQueryState> QueryStateTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsClipGate> PhysicsClipGateTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimerData> TimerDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimeTransform> TimeTransformTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;
            [ReadOnly] public ComponentLookup<FactionMember> FactionLookup;
            [ReadOnly] public ComponentLookup<TriggerQueryZoneTag> ZoneTagLookup;
            [ReadOnly] public ComponentLookup<TriggerQueryExposure> ExposureLookup;
            [ReadOnly] public ComponentLookup<TriggerQueryTaunt> TauntLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public bool HasCollisionWorld;
            [ReadOnly] public CollisionWorld CollisionWorld;
            public float ElapsedTime;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Events.BeginForEachIndex(unfilteredChunkIndex);

                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref QueryDataTypeHandle);
                var filters = chunk.GetNativeArray(ref FilterDataTypeHandle);
                var states = chunk.GetNativeArray(ref QueryStateTypeHandle);

                var gates = chunk.GetNativeArray(ref PhysicsClipGateTypeHandle);
                var hasTiming = chunk.Has(ref TimerDataTypeHandle) && chunk.Has(ref TimeTransformTypeHandle);
                var timers = hasTiming ? chunk.GetNativeArray(ref TimerDataTypeHandle) : default;
                var timeTransforms = hasTiming ? chunk.GetNativeArray(ref TimeTransformTypeHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var self = bindings[i].Value;
                    if (self == Entity.Null) continue;

                    var config = configs[i];
                    var filter = filters[i];
                    var queryState = states[i];

                    var isFirstFrame = gates[i].FirstFrame != 0;
                    if (isFirstFrame)
                    {
                        queryState = default;
                        queryState.LastSector = -1;
                    }

                    var isLastFrame = gates[i].LastFrame != 0;
                    var normalizedTime = 0.5f;
                    var prevNormalizedTime = 0.5f;
                    if (hasTiming)
                    {
                        normalizedTime = NormalizedClipTime(timers[i], timeTransforms[i]);
                        prevNormalizedTime =
                            NormalizedClipTimeAt(timers[i].Time - timers[i].DeltaTime, timeTransforms[i]);
                    }

                    // FrameWindowGate: a hard whole-query gate on clip-normalized time. Crossing-aware over the
                    // timer's [previous, current] interval so a low-FPS step that jumps the whole window still opens it.
                    var windowLo = math.min(prevNormalizedTime, normalizedTime);
                    var windowHi = math.max(prevNormalizedTime, normalizedTime);
                    var windowOpen = !FlagSet(config.Gates, PhysicsTriggerGateFlags.FrameWindowGate) ||
                                     (windowHi >= config.FrameWindowStart &&
                                      windowLo <= config.FrameWindowEnd);

                    if (!PhysicsMath.TryResolveTransform(self, in LocalTransformLookup, in LtwLookup,
                            in ParentLookup, out var selfPos, out var selfRot))
                    {
                        states[i] = queryState;
                        continue;
                    }

                    var forward = math.rotate(selfRot, math.forward());
                    if (!math.all(math.isfinite(forward)))
                    {
                        states[i] = queryState;
                        continue;
                    }

                    var selfVel = VelocityLookup.TryGetComponent(self, out var sv) ? sv.Linear : float3.zero;

                    var maxDistSq = config.MaxDistance > 0f
                        ? config.MaxDistance * config.MaxDistance
                        : float.MaxValue;
                    var minAlignment = config.MaxAngle > 0f ? math.cos(config.MaxAngle) : float.MinValue;
                    var targets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;

                    var ctx = new QueryContext
                    {
                        Self = self,
                        SelfPos = selfPos,
                        SelfRot = selfRot,
                        SelfVel = selfVel,
                        Forward = forward,
                        MaxDistSq = maxDistSq,
                        MinAlignment = minAlignment,
                        IsFirstFrame = isFirstFrame,
                        IsLastFrame = isLastFrame,
                        WindowOpen = windowOpen,
                        RaycastsLeft = config.MaxRaycastsPerQuery > 0 ? config.MaxRaycastsPerQuery : int.MaxValue,
                        NormalizedTime = normalizedTime,
                        ElapsedTime = ElapsedTime
                    };

                    var isMulti = config.Selection == PhysicsTriggerQuerySelection.AllSurvivorsFanout ||
                                  config.Selection == PhysicsTriggerQuerySelection.TopK;

                    if (isMulti)
                        ProcessMulti(in ctx, in config, in filter, in targets, ref queryState, timers, timeTransforms,
                            hasTiming, i);
                    else
                        ProcessSingle(in ctx, in config, in filter, in targets, ref queryState, timers, timeTransforms,
                            hasTiming, i);

                    PruneTracked(ref queryState);
                    states[i] = queryState;
                }

                Events.EndForEachIndex();
            }

            private struct QueryContext
            {
                public Entity Self;
                public float3 SelfPos;
                public quaternion SelfRot;
                public float3 SelfVel;
                public float3 Forward;
                public float MaxDistSq;
                public float MinAlignment;
                public bool IsFirstFrame;
                public bool IsLastFrame;
                public bool WindowOpen;
                public int RaycastsLeft;
                public float NormalizedTime; // clip-local [0,1] for TimingWindowGrade
                public float ElapsedTime; // world elapsed time for TauntOverride expiry
            }

            /// <summary>
            /// Per-candidate collision-stream data threaded through gating / scoring / value. For trigger-sourced
            /// candidates (no collision event) <see cref="HasCollision"/> is false and the ‡ modes no-op / sentinel.
            /// </summary>
            private struct CollisionInfo
            {
                public bool HasCollision; // a StatefulCollisionEvent for this candidate this frame
                public float3 Normal; // contact normal (valid whenever HasCollision)
                public bool HasDetails; // CalculateDetails was enabled → EstimatedImpulse is meaningful
                public float EstimatedImpulse;
            }

            private struct Candidate
            {
                public Entity Entity;
                public float Score;
                public float3 Offset;
                public float DistSq;
                public CollisionInfo Collision;
            }

            // ----------------------------------------------------------------------------------------------
            // SINGLE-WINNER path (Nearest .. DwellSelect)
            // ----------------------------------------------------------------------------------------------

            private void ProcessSingle(in QueryContext ctx, in PhysicsTriggerQueryData config,
                in PhysicsTriggerFilterData filter, in Targets targets, ref PhysicsTriggerQueryState queryState,
                NativeArray<TimerData> timers, NativeArray<TimeTransform> timeTransforms, bool hasTiming, int i)
            {
                var best = new Candidate { Entity = Entity.Null };
                var incumbent = new Candidate { Entity = Entity.Null };
                var survivorCount = 0;
                var raycastsLeft = ctx.RaycastsLeft;

                // TabCycle needs the FULL survivor set ordered by a stable key (R5 note); collected only for it.
                var isTabCycle = config.Selection == PhysicsTriggerQuerySelection.TabCycle;
                var survivors = new FixedList64Bytes<Entity>();
                var survivorKeys = new FixedList128Bytes<float>();

                if (ctx.WindowOpen)
                {
                    if (TriggerEventsLookup.TryGetBuffer(ctx.Self, out var triggers))
                        foreach (var evt in triggers)
                            Consider(in ctx, evt.EntityB, evt.State, default, in config, in filter, in targets,
                                queryState.LastWinner, ref best, ref incumbent, ref queryState, ref survivorCount,
                                ref raycastsLeft, isTabCycle, ref survivors, ref survivorKeys);

                    if (CollisionEventsLookup.TryGetBuffer(ctx.Self, out var collisions))
                        foreach (var evt in collisions)
                            Consider(in ctx, evt.EntityB, evt.State, ToCollisionInfo(evt), in config, in filter,
                                in targets, queryState.LastWinner, ref best, ref incumbent, ref queryState,
                                ref survivorCount, ref raycastsLeft, isTabCycle, ref survivors, ref survivorKeys);
                }

                // STABILITY — StickyWinner.
                var winner = best.Entity;
                if (incumbent.Entity != Entity.Null && config.SwitchMargin != 0)
                {
                    var margin = config.SwitchMargin / 100f;
                    if (best.Entity == Entity.Null || best.Score <= incumbent.Score + margin)
                        winner = incumbent.Entity;
                }

                // SELECTION — TabCycle: pick the successor of LastWinner in the stable (angle, index) ordering,
                // advancing on the re-fire edge. NOTE: the cycle breaks (restarts from the head) if the survivor
                // set changes mid-cycle, because the prior ordering is recomputed each frame from live survivors.
                if (isTabCycle)
                    winner = TabCycleSuccessor(in queryState, ref survivors, ref survivorKeys);

                // STABILITY — PerTargetRefractory: a winner still in refractory can't win again this acquisition.
                if (winner != Entity.Null && config.PerTargetRefractoryFrames > 0 &&
                    IsInRefractory(in queryState, winner))
                    winner = Entity.Null;

                // STABILITY — DwellToAcquire: a candidate must have survived gating N consecutive frames to win.
                if (winner != Entity.Null && config.DwellToAcquireFrames > 0 &&
                    !PhysicsTriggerSectorMath.DwellAcquired((ushort)math.min(GetDwell(winner, in queryState),
                        ushort.MaxValue), config.DwellToAcquireFrames))
                    winner = Entity.Null;

                // The winner's collision data (for ‡ value modes) — known when the winner is this frame's best/incumbent.
                var winnerCollision = winner == best.Entity ? best.Collision :
                    winner == incumbent.Entity ? incumbent.Collision : default;

                ProcessWinner(in ctx, ctx.Self, winner, in winnerCollision, in config, in targets, survivorCount,
                    ref queryState);
            }

            private void Consider(in QueryContext ctx, Entity other, StatefulEventState eventState,
                CollisionInfo collision, in PhysicsTriggerQueryData config, in PhysicsTriggerFilterData filter,
                in Targets targets, Entity incumbentEntity, ref Candidate best, ref Candidate incumbent,
                ref PhysicsTriggerQueryState queryState, ref int survivorCount, ref int raycastsLeft,
                bool collectSurvivors, ref FixedList64Bytes<Entity> survivors, ref FixedList128Bytes<float> survivorKeys)
            {
                if (!GateCandidate(in ctx, other, eventState, in collision, in config, in filter, in targets,
                        ref raycastsLeft, out var offset, out var distSq, out var alignment, out var otherLtw))
                    return;

                survivorCount++;
                MarkTracked(ref queryState, other);

                var score = ScoreCandidate(in ctx, other, in collision, in config, in targets, offset, distSq,
                    alignment, otherLtw, in queryState, ref raycastsLeft);

                var candidate = new Candidate
                {
                    Entity = other, Score = score, Offset = offset, DistSq = distSq, Collision = collision
                };

                if (other == incumbentEntity)
                    incumbent = candidate;

                if (best.Entity == Entity.Null ||
                    score > best.Score ||
                    (score == best.Score && IsLowerEntity(other, best.Entity)))
                    best = candidate;

                // TabCycle: record the survivor with its stable angle key (capped — drops past capacity, documented).
                if (collectSurvivors && survivors.Length < survivors.Capacity)
                {
                    survivors.Add(other);
                    survivorKeys.Add(TabCycleKey(in ctx, in config, offset));
                }
            }

            private static CollisionInfo ToCollisionInfo(in StatefulCollisionEvent evt)
            {
                var info = new CollisionInfo { HasCollision = true, Normal = evt.Normal };
                if (evt.TryGetDetails(out var details))
                {
                    info.HasDetails = true;
                    info.EstimatedImpulse = details.EstimatedImpulse;
                }

                return info;
            }

            // ----------------------------------------------------------------------------------------------
            // MULTI-WINNER path (AllSurvivorsFanout, TopK)
            // ----------------------------------------------------------------------------------------------

            private void ProcessMulti(in QueryContext ctx, in PhysicsTriggerQueryData config,
                in PhysicsTriggerFilterData filter, in Targets targets, ref PhysicsTriggerQueryState queryState,
                NativeArray<TimerData> timers, NativeArray<TimeTransform> timeTransforms, bool hasTiming, int i)
            {
                var cap = math.clamp(config.MaxTargets <= 0 ? 1 : config.MaxTargets, 1, 8);
                var raycastsLeft = ctx.RaycastsLeft;

                // Insertion-sorted top-cap by score (TopK). For AllSurvivorsFanout we still cap (drop past cap).
                var winners = new FixedList64Bytes<Entity>();
                var scores = new FixedList128Bytes<float>();
                var sectors = new FixedList128Bytes<int>();
                var bands = new FixedList128Bytes<int>();
                var values = new FixedList128Bytes<int>();
                var centroidSum = float3.zero;
                var survivorCount = 0;

                if (ctx.WindowOpen)
                {
                    if (TriggerEventsLookup.TryGetBuffer(ctx.Self, out var triggers))
                        foreach (var evt in triggers)
                            ConsiderMulti(in ctx, evt.EntityB, evt.State, default, in config, in filter, in targets,
                                cap, ref winners, ref scores, ref sectors, ref bands, ref values, ref centroidSum,
                                ref queryState, ref survivorCount, ref raycastsLeft);

                    if (CollisionEventsLookup.TryGetBuffer(ctx.Self, out var collisions))
                        foreach (var evt in collisions)
                            ConsiderMulti(in ctx, evt.EntityB, evt.State, ToCollisionInfo(evt), in config, in filter,
                                in targets, cap, ref winners, ref scores, ref sectors, ref bands, ref values,
                                ref centroidSum, ref queryState, ref survivorCount, ref raycastsLeft);
                }

                EmitMulti(in ctx, in config, in targets, ref queryState, in winners, in sectors, in bands, in values,
                    in scores, centroidSum, survivorCount);
            }

            private void ConsiderMulti(in QueryContext ctx, Entity other, StatefulEventState eventState,
                CollisionInfo collision, in PhysicsTriggerQueryData config, in PhysicsTriggerFilterData filter,
                in Targets targets, int cap, ref FixedList64Bytes<Entity> winners, ref FixedList128Bytes<float> scores,
                ref FixedList128Bytes<int> sectors, ref FixedList128Bytes<int> bands, ref FixedList128Bytes<int> values,
                ref float3 centroidSum, ref PhysicsTriggerQueryState queryState, ref int survivorCount,
                ref int raycastsLeft)
            {
                if (!GateCandidate(in ctx, other, eventState, in collision, in config, in filter, in targets,
                        ref raycastsLeft, out var offset, out var distSq, out var alignment, out var otherLtw))
                    return;

                survivorCount++;
                MarkTracked(ref queryState, other);
                centroidSum += otherLtw.Position; // AggregateCentroid: Σpos over ALL survivors (pre-cap)

                var score = ScoreCandidate(in ctx, other, in collision, in config, in targets, offset, distSq,
                    alignment, otherLtw, in queryState, ref raycastsLeft);

                // Insertion sort descending by score; cap at `cap` (drop the lowest past cap — documented).
                var insertAt = winners.Length;
                for (var k = 0; k < winners.Length; k++)
                    if (score > scores[k])
                    {
                        insertAt = k;
                        break;
                    }

                if (insertAt >= cap)
                    return; // worse than everything we keep; dropped past cap

                winners.InsertRangeWithBeginEnd(insertAt, insertAt + 1);
                scores.InsertRangeWithBeginEnd(insertAt, insertAt + 1);
                sectors.InsertRangeWithBeginEnd(insertAt, insertAt + 1);
                bands.InsertRangeWithBeginEnd(insertAt, insertAt + 1);
                values.InsertRangeWithBeginEnd(insertAt, insertAt + 1);

                var winnerPos = otherLtw.Position;
                var off = winnerPos - ctx.SelfPos;
                var sector = ComputeSectorOnly(in config, off, ctx.SelfRot);
                var band = ComputeBand(in config, math.lengthsq(off));
                winners[insertAt] = other;
                scores[insertAt] = score;
                sectors[insertAt] = sector;
                bands[insertAt] = band;
                // Pre-compute the per-survivor value now so collision-data (‡) modes get THIS body's contact.
                values[insertAt] = ComputeValueFor(in ctx, in config, other, in collision, ctx.SelfPos, ctx.SelfRot,
                    ctx.SelfVel, sector, band, survivorCount, ref queryState);

                while (winners.Length > cap)
                {
                    winners.RemoveAt(winners.Length - 1);
                    scores.RemoveAt(scores.Length - 1);
                    sectors.RemoveAt(sectors.Length - 1);
                    bands.RemoveAt(bands.Length - 1);
                    values.RemoveAt(values.Length - 1);
                }
            }

            private void EmitMulti(in QueryContext ctx, in PhysicsTriggerQueryData config, in Targets targets,
                ref PhysicsTriggerQueryState queryState, in FixedList64Bytes<Entity> winners,
                in FixedList128Bytes<int> sectors, in FixedList128Bytes<int> bands, in FixedList128Bytes<int> values,
                in FixedList128Bytes<float> scores, float3 centroidSum, int survivorCount)
            {
                var isTopK = config.Selection == PhysicsTriggerQuerySelection.TopK;
                var firstHit = true;

                // AggregateCentroid: the centroid's DirectionSector overrides every survivor's value (the vortex
                // pulls toward the swarm centre). A synthesized anchor entity isn't feasible in a Burst job (no
                // structural changes), so we emit the centroid SECTOR as the shared value and still route each
                // survivor into the slot — documented choice.
                var centroidSector = int.MinValue;
                if (survivorCount > 0)
                {
                    var centroid = centroidSum / survivorCount;
                    centroidSector = ComputeSectorOnly(in config, centroid - ctx.SelfPos, ctx.SelfRot);
                }

                for (var k = 0; k < winners.Length; k++)
                {
                    var w = winners[k];

                    // PerTargetRefractory.
                    if (config.PerTargetRefractoryFrames > 0 && IsInRefractory(in queryState, w))
                        continue;

                    // value: TopK = rank; AllSurvivorsFanout = the per-winner precomputed value.
                    var value = isTopK ? k : values[k];

                    // AggregateCentroid value override: emit the centroid bearing as the shared value.
                    if (config.ValueMode == PhysicsTriggerQueryValueMode.AggregateCentroid && centroidSector != int.MinValue)
                        value = centroidSector;

                    // TopK routes into an indexed slot offset; AllSurvivors uses the fixed route slot.
                    var slot = config.RouteSlot;

                    // Per-entity found EDGE: only fire foundCondition for a survivor that wasn't in last frame's
                    // set (mirrors the single-winner winner != LastWinner edge). The hit buffer still snapshots ALL
                    // current survivors below, regardless of edge.
                    var isNewThisFrame = !ContainsEntity(in queryState.LastWinnerSet, w);
                    var fireFound = isNewThisFrame ? config.FoundCondition : ConditionKey.Null;

                    // RedirectToLinkedRole §: route through THIS survivor's outbound EntityLink (pet → master).
                    var routeTo = config.RouteTo;
                    var routeLinkKey = config.RouteLinkKey;
                    if (config.RouteMode == PhysicsTriggerRouteMode.LinkedRole)
                    {
                        routeTo = Target.Target; // Target == the survivor (other) in TryResolveLinkedTarget
                        routeLinkKey = config.RedirectLinkKey;
                    }

                    if (PhysicsTriggerResolution.TryResolveLinkedTarget(routeTo, routeLinkKey,
                            ctx.Self, w, targets, LinkSources, Links, out var routed))
                        Events.Write(new TriggerQueryEvent
                        {
                            Routed = routed,
                            Winner = w,
                            Self = Entity.Null,
                            WriteSlot = true,
                            ClearHitBuffer = config.WriteHitBuffer && firstHit,
                            WriteHit = config.WriteHitBuffer,
                            Slot = slot,
                            WriteMode = config.WriteMode,
                            Condition = fireFound,
                            Value = value,
                            Sector = sectors[k],
                            Band = bands[k],
                            Score = (int)(scores[k] * 100f)
                        });

                    if (config.PerTargetRefractoryFrames > 0)
                        SetRefractory(ref queryState, w, config.PerTargetRefractoryFrames);

                    firstHit = false;
                }

                // Multi-winner lost edge: bodies in last frame's set that are gone now fire the lost condition.
                if (!config.LostCondition.Equals(ConditionKey.Null))
                    for (var k = 0; k < queryState.LastWinnerSet.Length; k++)
                    {
                        var prev = queryState.LastWinnerSet[k];
                        if (prev == Entity.Null || ContainsEntity(in winners, prev))
                            continue;

                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey,
                                ctx.Self, prev, targets, LinkSources, Links, out var lostRouted))
                            Events.Write(new TriggerQueryEvent
                            {
                                Routed = lostRouted,
                                Winner = Entity.Null,
                                WriteSlot = false,
                                Condition = config.LostCondition,
                                Value = config.LostValue
                            });
                    }

                // Persist the new winner set (capped at FixedList64Bytes capacity).
                queryState.LastWinnerSet.Clear();
                for (var k = 0; k < winners.Length && queryState.LastWinnerSet.Length < queryState.LastWinnerSet.Capacity; k++)
                    queryState.LastWinnerSet.Add(winners[k]);

                queryState.PrevCount = survivorCount;
            }

            private static bool ContainsEntity(in FixedList64Bytes<Entity> list, Entity e)
            {
                for (var k = 0; k < list.Length; k++)
                    if (list[k] == e)
                        return true;
                return false;
            }

            // ----------------------------------------------------------------------------------------------
            // GATING (shared by single + multi)
            // ----------------------------------------------------------------------------------------------

            private bool GateCandidate(in QueryContext ctx, Entity other, StatefulEventState eventState,
                in CollisionInfo collision, in PhysicsTriggerQueryData config, in PhysicsTriggerFilterData filter,
                in Targets targets, ref int raycastsLeft, out float3 offset, out float distSq, out float alignment,
                out LocalToWorld otherLtw)
            {
                offset = default;
                distSq = 0f;
                alignment = 0f;
                otherLtw = default;

                if (other == Entity.Null) return false;
                if (!StatefulEventMatching.Matches(eventState, config.EventState, ctx.IsFirstFrame, ctx.IsLastFrame))
                    return false;

                // GATING ExcludeRoles — broader than ignoreTarget; skip any routed-role entity.
                if (config.ExcludeRoles != PhysicsTriggerRoleMask.None &&
                    IsExcludedRole(other, ctx.Self, in targets, config.ExcludeRoles))
                    return false;

                if (config.CollidesWithMask != 0)
                {
                    if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return false;
                    if ((collider.Value.Value.GetCollisionFilter().BelongsTo & config.CollidesWithMask) == 0)
                        return false;
                }

                if (!PhysicsTriggerFiltering.IsValidTarget(ctx.Self, other, in filter, in targets, LinkSources, Links))
                    return false;

                if (!LtwLookup.TryGetComponent(other, out otherLtw)) return false;

                offset = otherLtw.Position - ctx.SelfPos;
                distSq = math.lengthsq(offset);
                if (distSq > ctx.MaxDistSq) return false;

                alignment = distSq > 1e-8f ? math.dot(ctx.Forward, offset * math.rsqrt(distSq)) : 1f;
                if (alignment < ctx.MinAlignment) return false;

                // ---- WAVE 2 gates ----

                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.ApproachGate))
                {
                    var otherVel = VelocityLookup.TryGetComponent(other, out var ov) ? ov.Linear : float3.zero;
                    var closing = PhysicsTriggerSectorMath.ClosingSpeed(ctx.SelfVel, otherVel, offset);
                    if (config.ApproachRecedingOnly)
                    {
                        if (closing > -config.MinClosingSpeed) return false;
                    }
                    else if (closing < config.MinClosingSpeed)
                    {
                        return false;
                    }
                }

                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.FacingGate))
                {
                    var otherRot = ResolveRotation(other);
                    var otherFwd = math.rotate(otherRot, new float3(0f, 0f, 1f));
                    var toSelf = ctx.SelfPos - otherLtw.Position;
                    var lenSq = math.lengthsq(toSelf);
                    var d = lenSq > 1e-8f
                        ? math.dot(otherFwd, toSelf * math.rsqrt(lenSq))
                        : 1f;
                    // back-turned: candidate's forward points AWAY from self → dot(fwd, toSelf) < -cos.
                    // face-to-face: candidate's forward points TOWARD self → dot(fwd, toSelf) > cos.
                    var pass = config.FacingFaceToFace
                        ? d > config.FacingCosThreshold
                        : d < -config.FacingCosThreshold;
                    if (!pass) return false;
                }

                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.VerticalGate))
                {
                    var tier = PhysicsTriggerSectorMath.VerticalTier(offset.y, config.VerticalMidLow,
                        config.VerticalMidHigh);
                    if ((config.VerticalTierMask & (1 << tier)) == 0) return false;
                }

                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.MassBracket))
                {
                    var hasMass = MassLookup.TryGetComponent(other, out var mass);
                    if (!hasMass)
                    {
                        if (!config.MassIncludeStatic) return false; // static → no PhysicsMass
                        // static treated as InverseMass 0
                        if (0f < config.MassInvMin || 0f > config.MassInvMax) return false;
                    }
                    else if (mass.InverseMass < config.MassInvMin || mass.InverseMass > config.MassInvMax)
                    {
                        return false;
                    }
                }

                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.FactionGate))
                {
                    // External binding: games assign FactionMember. Missing component → treated as faction 0.
                    var faction = FactionLookup.TryGetComponent(other, out var fm) ? fm.Faction : 0;
                    if (faction is >= 0 and < 32)
                    {
                        if ((config.FactionAllowMask & (1u << faction)) == 0) return false;
                    }
                    else if (config.FactionAllowMask != 0)
                    {
                        return false; // out-of-range faction can't match a non-empty mask
                    }
                }

                // LoS family — compute ONE ray, route to gate (RequireOccluded inverts) and reuse below for value.
                if ((config.RequireLineOfSight || FlagSet(config.Gates, PhysicsTriggerGateFlags.RequireOccluded)) &&
                    HasCollisionWorld)
                {
                    if (raycastsLeft <= 0) return false; // budget exhausted → fail closed
                    raycastsLeft--;
                    var clear = TeleportMath.CheckLineOfSight(in CollisionWorld, ctx.SelfPos, otherLtw.Position,
                        config.LineOfSightOffset, config.ObstacleMask, ctx.Self, other);

                    if (config.RequireLineOfSight && !clear) return false;
                    if (FlagSet(config.Gates, PhysicsTriggerGateFlags.RequireOccluded) && clear) return false;
                }

                // ---- WAVE 3 gates ----

                // ZoneStateGate † — require (or, inverted, exclude) the enableable TriggerQueryZoneTag.
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.ZoneStateGate))
                {
                    // TODO external zone binding: games enable/disable TriggerQueryZoneTag on their bodies.
                    var tagged = ZoneTagLookup.HasComponent(other) && ZoneTagLookup.IsComponentEnabled(other);
                    if (tagged == config.ZoneStateInvert) return false;
                }

                // LightExposureGate † — gate by an external illumination value vs a threshold.
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.LightExposureGate))
                {
                    // TODO external light system binding: a downstream system writes TriggerQueryExposure.Value.
                    var exposure = ExposureLookup.TryGetComponent(other, out var ex) ? ex.Value : 0f;
                    var bright = exposure >= config.LightExposureThreshold;
                    if (bright == config.LightExposureInvert) return false;
                }

                // SurfaceMaterialGate ‡ — gate by the collision contact filter bits. Passthrough on a pure trigger.
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.SurfaceMaterialGate))
                {
                    // ‡ collision-stream data. No collision event this frame → no-op (passthrough). The contact
                    // material proxy here is the candidate collider's BelongsTo (no per-contact material in the
                    // stateful event). TODO bind a real contact-material channel when one is available.
                    if (collision.HasCollision && config.SurfaceMaterialMask != 0)
                    {
                        var belongsTo = 0u;
                        if (ColliderLookup.TryGetComponent(other, out var c) && c.IsValid)
                            belongsTo = c.Value.Value.GetCollisionFilter().BelongsTo;
                        if ((belongsTo & config.SurfaceMaterialMask) == 0) return false;
                    }
                }

                // DraftCorridorGate — keep only candidates in the rear slipstream capsule behind the LEADER (other).
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.DraftCorridorGate))
                {
                    var leaderFwd = math.rotate(ResolveRotation(other), new float3(0f, 0f, 1f));
                    // Project self's offset onto -leaderForward (behind the leader) and bound the perpendicular.
                    var toSelf = ctx.SelfPos - otherLtw.Position;
                    var behind = math.dot(toSelf, -leaderFwd); // > 0 → self is behind the leader
                    if (behind < 0f || behind > config.DraftCorridorLength) return false;
                    var perp = toSelf + leaderFwd * behind; // remove the along-axis component
                    if (math.lengthsq(perp) > config.DraftCorridorRadius * config.DraftCorridorRadius) return false;
                }

                // PassLaneCone — a second cone whose axis is (refPos - selfPos); keep candidates inside it.
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.PassLaneCone))
                {
                    if (!PhysicsTriggerResolution.TryResolveLinkedTarget(config.PassLaneRefTarget,
                            config.PassLaneRefLinkKey, ctx.Self, other, targets, LinkSources, Links, out var refE) ||
                        !LtwLookup.TryGetComponent(refE, out var refLtw))
                        return false; // no reference → can't form the lane
                    var axis = refLtw.Position - ctx.SelfPos;
                    var axisLenSq = math.lengthsq(axis);
                    if (axisLenSq < 1e-8f || distSq < 1e-8f) return false;
                    var cos = math.dot(axis * math.rsqrt(axisLenSq), offset * math.rsqrt(distSq));
                    if (cos < config.PassLaneConeCos) return false;
                }

                // LedgeGate — short down-ray from the candidate; exclude bodies standing on ground (counts budget).
                if (FlagSet(config.Gates, PhysicsTriggerGateFlags.LedgeGate) && HasCollisionWorld)
                {
                    if (raycastsLeft <= 0) return false; // budget exhausted → fail closed
                    raycastsLeft--;
                    var from = otherLtw.Position;
                    var to = from + new float3(0f, -math.max(config.LedgeRayDepth, Epsilon), 0f);
                    var hitGround = !TeleportMath.CheckLineOfSight(in CollisionWorld, from, to, 0f,
                        config.ObstacleMask, ctx.Self, other);
                    if (hitGround) return false; // ground within depth → NOT over a void
                }

                return true;
            }

            private const float Epsilon = 1e-6f;

            private static bool IsExcludedRole(Entity other, Entity self, in Targets targets,
                PhysicsTriggerRoleMask mask)
            {
                if ((mask & PhysicsTriggerRoleMask.Self) != 0 && other == self) return true;
                if ((mask & PhysicsTriggerRoleMask.Owner) != 0 && other == targets.Owner) return true;
                if ((mask & PhysicsTriggerRoleMask.Source) != 0 && other == targets.Source) return true;
                if ((mask & PhysicsTriggerRoleMask.Target) != 0 && other == targets.Target) return true;
                return false;
            }

            // ----------------------------------------------------------------------------------------------
            // SELECTION scoring (shared)
            // ----------------------------------------------------------------------------------------------

            private float ScoreCandidate(in QueryContext ctx, Entity other, in CollisionInfo collision,
                in PhysicsTriggerQueryData config, in Targets targets, float3 offset, float distSq, float alignment,
                in LocalToWorld otherLtw, in PhysicsTriggerQueryState queryState, ref int raycastsLeft)
            {
                switch (config.Selection)
                {
                    case PhysicsTriggerQuerySelection.Nearest:
                        return -distSq;
                    case PhysicsTriggerQuerySelection.Farthest:
                        return distSq;
                    case PhysicsTriggerQuerySelection.MostAligned:
                        return alignment;
                    case PhysicsTriggerQuerySelection.LeastAligned:
                        return -alignment;

                    case PhysicsTriggerQuerySelection.HighestThreat:
                    {
                        var proximity = ctx.MaxDistSq < float.MaxValue && ctx.MaxDistSq > 0f
                            ? 1f - math.saturate(distSq / ctx.MaxDistSq)
                            : 1f / (1f + distSq);
                        var threat = ReadThreatStat(in config, other);
                        return config.ThreatWeightDist * proximity +
                               config.ThreatWeightAlign * alignment +
                               config.ThreatWeightStat * threat;
                    }

                    case PhysicsTriggerQuerySelection.WeakestTarget:
                    {
                        // score = -statValue; a body with NO stat component never wins unless alone.
                        if (!TryReadStatRaw(in config, other, out var v))
                            return float.NegativeInfinity;
                        return -v;
                    }

                    case PhysicsTriggerQuerySelection.CategoryPriority:
                    {
                        var ord = ReadCategoryOrdinal(in config, other);
                        // tie-break by proximity (geometric) folded into the fractional part.
                        var prox = 1f / (1f + distSq);
                        return ord + prox * 0.001f;
                    }

                    case PhysicsTriggerQuerySelection.ClosingSpeedSelect:
                    {
                        var otherVel = VelocityLookup.TryGetComponent(other, out var ov) ? ov.Linear : float3.zero;
                        var closing = PhysicsTriggerSectorMath.ClosingSpeed(ctx.SelfVel, otherVel, offset);
                        return config.ClosingSpeedFleeing ? -closing : closing;
                    }

                    case PhysicsTriggerQuerySelection.MostExposed:
                    {
                        // Graded LoS openness — can't early out. Obey budget; no ray left → least exposed.
                        if (!HasCollisionWorld || raycastsLeft <= 0)
                            return 0f;
                        raycastsLeft--;
                        var clear = TeleportMath.CheckLineOfSight(in CollisionWorld, ctx.SelfPos, otherLtw.Position,
                            config.LineOfSightOffset, config.ObstacleMask, ctx.Self, other);
                        return clear ? 1f : 0f;
                    }

                    case PhysicsTriggerQuerySelection.DwellSelect:
                    {
                        var dwell = GetDwell(other, in queryState);
                        return config.DwellDescending ? dwell : -dwell;
                    }

                    case PhysicsTriggerQuerySelection.HeaviestMover:
                    {
                        // score = mass^a * |v|^b. Shares PhysicsMass (mass = 1/InverseMass) + PhysicsVelocity reads.
                        var mass = 0f;
                        if (MassLookup.TryGetComponent(other, out var pm) && pm.InverseMass > PhysicsTriggerSectorMath.Epsilon)
                            mass = 1f / pm.InverseMass;
                        var speed = VelocityLookup.TryGetComponent(other, out var pv) ? math.length(pv.Linear) : 0f;
                        return PhysicsTriggerSectorMath.HeaviestMoverScore(mass, speed, config.HeaviestMassExp,
                            config.HeaviestSpeedExp);
                    }

                    case PhysicsTriggerQuerySelection.MostBlocking:
                    {
                        // score = -perpendicular distance to the self→referenceTarget segment (closest to the line wins).
                        if (!PhysicsTriggerResolution.TryResolveLinkedTarget(config.BlockingRefTarget,
                                config.BlockingRefLinkKey, ctx.Self, other, targets, LinkSources, Links, out var refE) ||
                            !LtwLookup.TryGetComponent(refE, out var refLtw))
                            return float.NegativeInfinity; // no reference segment → never the blocker
                        var perp = PhysicsTriggerSectorMath.PerpendicularDistance(otherLtw.Position, ctx.SelfPos,
                            refLtw.Position - ctx.SelfPos);
                        return -perp;
                    }

                    case PhysicsTriggerQuerySelection.TauntOverride:
                    {
                        // † A candidate with an unexpired taunt instantly wins, locked. +inf so it dominates the
                        // reduction; ties broken by entity index. TODO game taunt binding (set UntilTime to world time).
                        if (TauntLookup.TryGetComponent(other, out var taunt) && taunt.UntilTime > ctx.ElapsedTime)
                            return float.PositiveInfinity;
                        return -distSq; // un-taunted bodies fall back to nearest
                    }

                    case PhysicsTriggerQuerySelection.TabCycle:
                        // TabCycle does its successor pick in ProcessSingle from the collected survivor ordering;
                        // the score reduction is unused for the pick (nearest is a harmless default).
                        return -distSq;

                    case PhysicsTriggerQuerySelection.AllSurvivorsFanout:
                    case PhysicsTriggerQuerySelection.TopK:
                        // Multi modes sort by proximity by default (rank meaningful, nearest first).
                        return -distSq;

                    default:
                        return -distSq;
                }
            }

            private float ReadThreatStat(in PhysicsTriggerQueryData config, Entity other)
            {
                return TryReadStatRaw(in config, other, out var v) ? v : 0f;
            }

            private bool TryReadStatRaw(in PhysicsTriggerQueryData config, Entity other, out float value)
            {
                value = 0f;
                if (!config.ThreatStat.IsEnabled()) return false;
                if (!StatLookup.TryGetBuffer(other, out var stats)) return false;
                value = stats.AsMap().GetValueFloat(config.ThreatStat.Stat, 0f);
                return true;
            }

            private int ReadCategoryOrdinal(in PhysicsTriggerQueryData config, Entity other)
            {
                if (!config.CategoryTiers.IsCreated) return 0;
                if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return 0;
                var belongsTo = collider.Value.Value.GetCollisionFilter().BelongsTo;
                ref var tiers = ref config.CategoryTiers.Value;
                return PhysicsTriggerSectorMath.CategoryOrdinal(belongsTo, ref tiers.Masks, ref tiers.Ordinals, 0);
            }

            // ----------------------------------------------------------------------------------------------
            // Tracked per-candidate state (dwell + refractory)
            // ----------------------------------------------------------------------------------------------

            private static int FindTracked(in PhysicsTriggerQueryState state, Entity e)
            {
                for (var k = 0; k < state.Tracked.Length; k++)
                    if (state.Tracked[k].Entity == e)
                        return k;
                return -1;
            }

            private static void MarkTracked(ref PhysicsTriggerQueryState state, Entity e)
            {
                var idx = FindTracked(in state, e);
                if (idx >= 0)
                {
                    var c = state.Tracked[idx];
                    c.Seen = 1;
                    if (c.Dwell < ushort.MaxValue) c.Dwell++;
                    if (c.Refractory > 0) c.Refractory--;
                    state.Tracked[idx] = c;
                    return;
                }

                if (state.Tracked.Length >= MaxTrackedCandidates)
                    return; // capped — drop (documented)

                state.Tracked.Add(new EntityCounter { Entity = e, Dwell = 1, Refractory = 0, Seen = 1 });
            }

            private static float GetDwell(Entity e, in PhysicsTriggerQueryState state)
            {
                var idx = FindTracked(in state, e);
                return idx >= 0 ? state.Tracked[idx].Dwell : 0;
            }

            private static bool IsInRefractory(in PhysicsTriggerQueryState state, Entity e)
            {
                var idx = FindTracked(in state, e);
                return idx >= 0 && state.Tracked[idx].Refractory > 0;
            }

            private static void SetRefractory(ref PhysicsTriggerQueryState state, Entity e, ushort frames)
            {
                var idx = FindTracked(in state, e);
                if (idx >= 0)
                {
                    var c = state.Tracked[idx];
                    c.Refractory = frames;
                    state.Tracked[idx] = c;
                }
            }

            private static void PruneTracked(ref PhysicsTriggerQueryState state)
            {
                for (var k = state.Tracked.Length - 1; k >= 0; k--)
                {
                    var c = state.Tracked[k];
                    if (c.Seen == 0 && c.Refractory == 0)
                    {
                        state.Tracked.RemoveAt(k); // gone and no pending refractory → forget
                        continue;
                    }

                    if (c.Seen == 0)
                        c.Dwell = 0; // absent but still cooling down — reset dwell, keep refractory

                    c.Seen = 0;
                    state.Tracked[k] = c;
                }
            }

            // ----------------------------------------------------------------------------------------------
            // WINNER / VALUE / ROUTE (single-winner)
            // ----------------------------------------------------------------------------------------------

            private void ProcessWinner(in QueryContext ctx, Entity self, Entity winner, in CollisionInfo winnerCollision,
                in PhysicsTriggerQueryData config, in Targets targets, int survivorCount,
                ref PhysicsTriggerQueryState queryState)
            {
                var selfPos = ctx.SelfPos;
                var selfRot = ctx.SelfRot;
                var selfVel = ctx.SelfVel;

                if (winner != Entity.Null)
                {
                    queryState.GraceCountdown = 0;

                    if (winner != queryState.LastWinner)
                    {
                        var off = ResolveWinnerPos(winner, selfPos) - selfPos;
                        var sector = ComputeSectorOnly(in config, off, selfRot);
                        var band = ComputeBand(in config, math.lengthsq(off));
                        var value = ComputeValueFor(in ctx, in config, winner, in winnerCollision, selfPos, selfRot,
                            selfVel, sector, band, survivorCount, ref queryState);

                        ResolveRouteSlot(in config, sector, band, value, out var routeSlot);

                        // RedirectToLinkedRole §: route through the WINNER's outbound EntityLink (pet → master).
                        var routeTo = config.RouteTo;
                        var routeLinkKey = config.RouteLinkKey;
                        if (config.RouteMode == PhysicsTriggerRouteMode.LinkedRole)
                        {
                            // Resolve the winner itself, then chase its outbound link; the ApplyJob still does the
                            // cross-entity Targets write single-threaded (serialization point preserved).
                            routeTo = Target.Target; // Target resolves to the winner (other) in TryResolveLinkedTarget
                            routeLinkKey = config.RedirectLinkKey;
                        }

                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(routeTo, routeLinkKey,
                                self, winner, targets, LinkSources, Links, out var routed))
                            Events.Write(new TriggerQueryEvent
                            {
                                Routed = routed,
                                Winner = winner,
                                // MirrorIntoWinner: ApplyJob also writes `self` into the winner's slot (RW Targets).
                                Self = config.MirrorIntoWinner ? self : Entity.Null,
                                WriteSlot = true,
                                Slot = routeSlot,
                                WriteMode = config.WriteMode,
                                Condition = config.FoundCondition,
                                Value = value
                            });

                        if (config.PerTargetRefractoryFrames > 0)
                            SetRefractory(ref queryState, winner, config.PerTargetRefractoryFrames);

                        queryState.LastWinner = winner;
                    }

                    queryState.LastKnownPos = ResolveWinnerPos(winner, selfPos);
                    queryState.PrevCount = survivorCount;
                    return;
                }

                queryState.PrevCount = survivorCount;

                if (queryState.LastWinner == Entity.Null)
                    return;

                if (config.GraceFrames > 0)
                {
                    if (queryState.GraceCountdown == 0)
                        queryState.GraceCountdown = config.GraceFrames;

                    queryState.GraceCountdown--;
                    if (queryState.GraceCountdown > 0)
                        return;
                }

                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey,
                        self, queryState.LastWinner, targets, LinkSources, Links, out var lostRouted))
                    Events.Write(new TriggerQueryEvent
                    {
                        Routed = lostRouted,
                        Winner = Entity.Null,
                        WriteSlot = config.ClearOnLost,
                        Slot = config.RouteSlot,
                        WriteMode = PhysicsTriggerWriteMode.ClearOnly,
                        Condition = config.LostCondition,
                        Value = config.LostValue
                    });

                queryState.LastWinner = Entity.Null;
                queryState.LastSector = -1;
            }

            private float3 ResolveWinnerPos(Entity winner, float3 fallback)
            {
                return LtwLookup.TryGetComponent(winner, out var ltw) ? ltw.Position : fallback;
            }

            private quaternion ResolveRotation(Entity e)
            {
                if (LtwLookup.TryGetComponent(e, out var ltw))
                {
                    var r = new float3x3(ltw.Value.c0.xyz, ltw.Value.c1.xyz, ltw.Value.c2.xyz);
                    r.c0 = math.normalizesafe(r.c0, new float3(1f, 0f, 0f));
                    r.c1 = math.normalizesafe(r.c1, new float3(0f, 1f, 0f));
                    r.c2 = math.normalizesafe(r.c2, new float3(0f, 0f, 1f));
                    return new quaternion(r);
                }

                if (LocalTransformLookup.TryGetComponent(e, out var lt))
                    return lt.Rotation;

                return quaternion.identity;
            }

            private void ResolveRouteSlot(in PhysicsTriggerQueryData config, int sector, int band, int value,
                out PhysicsTriggerRouteSlot slot)
            {
                if (config.RouteMode == PhysicsTriggerRouteMode.ByValue)
                {
                    // Map the computed value onto a slot deterministically: 0→Custom,1→Owner,2→Source,3→Target.
                    slot = (PhysicsTriggerRouteSlot)(((value % 4) + 4) % 4);
                    return;
                }

                slot = config.RouteSlot;
            }

            private int ComputeValueFor(in QueryContext ctx, in PhysicsTriggerQueryData config, Entity winner,
                in CollisionInfo collision, float3 selfPos, quaternion selfRot, float3 selfVel, int sector, int band,
                int survivorCount, ref PhysicsTriggerQueryState queryState)
            {
                switch (config.ValueMode)
                {
                    case PhysicsTriggerQueryValueMode.DirectionSector:
                        return ComputeSectorValue(in config, winner, selfPos, selfRot, ref queryState);

                    case PhysicsTriggerQueryValueMode.DistanceBand:
                    {
                        var off = ResolveWinnerPos(winner, selfPos) - selfPos;
                        return ComputeBand(in config, math.lengthsq(off));
                    }

                    case PhysicsTriggerQueryValueMode.SectorBandPacked:
                    {
                        var s = ComputeSectorValue(in config, winner, selfPos, selfRot, ref queryState);
                        var off = ResolveWinnerPos(winner, selfPos) - selfPos;
                        var b = ComputeBand(in config, math.lengthsq(off));
                        return b * config.SectorCount + s;
                    }

                    case PhysicsTriggerQueryValueMode.OverlapCount:
                    {
                        var count = survivorCount;
                        if (config.OverlapThresholdCross)
                        {
                            var crossed = count >= config.OverlapThreshold &&
                                          queryState.PrevCount < config.OverlapThreshold;
                            return crossed ? 1 : 0;
                        }

                        return count;
                    }

                    case PhysicsTriggerQueryValueMode.FacingSide:
                    {
                        var winnerPos = ResolveWinnerPos(winner, selfPos);
                        var otherRot = ResolveRotation(winner);
                        var otherFwd = math.rotate(otherRot, new float3(0f, 0f, 1f));
                        return PhysicsTriggerSectorMath.FacingSide(otherFwd, selfPos - winnerPos, 0.5f, -0.5f);
                    }

                    case PhysicsTriggerQueryValueMode.CategoryOrdinal:
                        return ReadCategoryOrdinal(in config, winner);

                    case PhysicsTriggerQueryValueMode.ApproachVelocityBand:
                    {
                        var winnerPos = ResolveWinnerPos(winner, selfPos);
                        var otherVel = VelocityLookup.TryGetComponent(winner, out var ov) ? ov.Linear : float3.zero;
                        var closing = PhysicsTriggerSectorMath.ClosingSpeed(selfVel, otherVel, winnerPos - selfPos);
                        return PhysicsTriggerSectorMath.ApproachVelocityBand(closing, config.ApproachBandWidth);
                    }

                    case PhysicsTriggerQueryValueMode.ScaledMagnitude:
                    {
                        var scalar = 0f;
                        if (config.ScaledMagnitudeStat.IsEnabled() &&
                            StatLookup.TryGetBuffer(winner, out var stats))
                            scalar = stats.AsMap().GetValueFloat(config.ScaledMagnitudeStat.Stat, 0f);

                        if (!config.MagnitudeBands.IsCreated) return 0;
                        return PhysicsTriggerSectorMath.Bucket(scalar, ref config.MagnitudeBands.Value.SquaredThresholds);
                    }

                    // ---- WAVE 3 ----

                    case PhysicsTriggerQueryValueMode.ContactNormalSector:
                    {
                        // ‡ No collision normal this frame → sentinel == SectorCount (never a false face 0).
                        if (!collision.HasCollision) return config.SectorCount;
                        var fwd = config.SectorReference == PhysicsTriggerSectorReference.World
                            ? new float3(0f, 0f, 1f)
                            : math.rotate(selfRot, new float3(0f, 0f, 1f));
                        var up = ResolveSectorUp(in config, selfRot);
                        return PhysicsTriggerSectorMath.NormalSector(collision.Normal, fwd, up, config.SectorCount);
                    }

                    case PhysicsTriggerQueryValueMode.ImpactBand:
                    {
                        // ‡ No collision details this frame → band 0 (no impact registered).
                        if (!collision.HasCollision || !collision.HasDetails || !config.ImpactBands.IsCreated)
                            return 0;
                        return PhysicsTriggerSectorMath.ImpactBand(collision.EstimatedImpulse,
                            ref config.ImpactBands.Value.SquaredThresholds);
                    }

                    case PhysicsTriggerQueryValueMode.TimingWindowGrade:
                        return PhysicsTriggerSectorMath.TimingWindowGrade(ctx.NormalizedTime, config.TimingBeatCenter,
                            config.TimingPerfect, config.TimingGreat, config.TimingGood);

                    case PhysicsTriggerQueryValueMode.OcclusionState:
                    {
                        // Reuses the SAME LoS ray result as RequireOccluded / MostExposed; budget already counted in
                        // gating when those modes are active. Here we compute on demand (subject to no budget left → 0).
                        if (!HasCollisionWorld) return 0;
                        var winnerPos = ResolveWinnerPos(winner, selfPos);
                        var clear = TeleportMath.CheckLineOfSight(in CollisionWorld, selfPos, winnerPos,
                            config.LineOfSightOffset, config.ObstacleMask, ctx.Self, winner);
                        return clear ? 0 : 1; // 0 = visible, 1 = hidden (occluded)
                    }

                    case PhysicsTriggerQueryValueMode.DeflectionBounce:
                    {
                        // ‡ Reflect self-velocity across the contact normal → sector. No normal → sentinel.
                        if (!collision.HasCollision) return config.SectorCount;
                        var fwd = config.SectorReference == PhysicsTriggerSectorReference.World
                            ? new float3(0f, 0f, 1f)
                            : math.rotate(selfRot, new float3(0f, 0f, 1f));
                        var up = ResolveSectorUp(in config, selfRot);
                        return PhysicsTriggerSectorMath.DeflectionSector(selfVel, collision.Normal, fwd, up,
                            config.SectorCount);
                    }

                    case PhysicsTriggerQueryValueMode.AggregateCentroid:
                        // MULTI-only; the centroid bearing is supplied by EmitMulti. Single-winner → Constant.
                        return config.FoundValue;

                    default:
                        return config.FoundValue;
                }
            }

            private int ComputeSectorOnly(in PhysicsTriggerQueryData config, float3 offset, quaternion selfRot)
            {
                var fwd = config.SectorReference == PhysicsTriggerSectorReference.World
                    ? new float3(0f, 0f, 1f)
                    : math.rotate(selfRot, new float3(0f, 0f, 1f));
                var up = ResolveSectorUp(in config, selfRot);
                return PhysicsTriggerSectorMath.ComputeSector(offset, fwd, up, config.SectorCount);
            }

            private int ComputeSectorValue(in PhysicsTriggerQueryData config, Entity winner, float3 selfPos,
                quaternion selfRot, ref PhysicsTriggerQueryState queryState)
            {
                var winnerPos = ResolveWinnerPos(winner, selfPos);
                var offset = winnerPos - selfPos;

                var fwd = config.SectorReference == PhysicsTriggerSectorReference.World
                    ? new float3(0f, 0f, 1f)
                    : math.rotate(selfRot, new float3(0f, 0f, 1f));
                var up = ResolveSectorUp(in config, selfRot);

                var raw = PhysicsTriggerSectorMath.ComputeRawSector(offset, fwd, up, config.SectorCount, out var angle);
                var sector = PhysicsTriggerSectorMath.ApplyHysteresis(raw, angle, queryState.LastSector,
                    config.SectorCount, config.SectorHysteresis);

                if (sector != config.SectorCount)
                    queryState.LastSector = (sbyte)sector;

                return sector;
            }

            private static float3 ResolveSectorUp(in PhysicsTriggerQueryData config, quaternion selfRot)
            {
                switch (config.SectorPlane)
                {
                    case PhysicsTriggerSectorPlane.ViewRelative:
                        return math.rotate(selfRot, new float3(0f, 1f, 0f));
                    case PhysicsTriggerSectorPlane.CustomAxis:
                        return config.SectorCustomUp;
                    default:
                        return new float3(0f, 1f, 0f);
                }
            }

            private static int ComputeBand(in PhysicsTriggerQueryData config, float distSq)
            {
                if (!config.DistanceBands.IsCreated)
                    return 0;

                return PhysicsTriggerSectorMath.Bucket(distSq, ref config.DistanceBands.Value.SquaredThresholds);
            }

            private static float NormalizedClipTime(in TimerData timer, in TimeTransform tt)
            {
                return NormalizedClipTimeAt(timer.Time, in tt);
            }

            private static float NormalizedClipTimeAt(DiscreteTime time, in TimeTransform tt)
            {
                var start = (double)tt.Start;
                var end = (double)tt.End;
                var span = end - start;
                if (span <= 0) return 0.5f;
                var n = ((double)time - start) / span;
                return (float)math.clamp(n, 0.0, 1.0);
            }

            private static bool IsLowerEntity(Entity a, Entity b)
            {
                return a.Index != b.Index ? a.Index < b.Index : a.Version < b.Version;
            }

            /// <summary> The stable TabCycle ordering key: the planar bearing angle [0, 2π). Ties broken by index. </summary>
            private float TabCycleKey(in QueryContext ctx, in PhysicsTriggerQueryData config, float3 offset)
            {
                var fwd = config.SectorReference == PhysicsTriggerSectorReference.World
                    ? new float3(0f, 0f, 1f)
                    : math.rotate(ctx.SelfRot, new float3(0f, 0f, 1f));
                var up = ResolveSectorUp(in config, ctx.SelfRot);
                PhysicsTriggerSectorMath.ComputeRawSector(offset, fwd, up, math.max(config.SectorCount, 1), out var angle);
                return float.IsNaN(angle) ? 0f : angle;
            }

            /// <summary>
            /// Pick the successor of LastWinner in the stable (angle, index) ordering of the live survivors. On the
            /// re-fire edge (LastWinner still present) advance to the next; if LastWinner is gone or null, start at the
            /// head. The cycle restarts if the survivor set changes mid-cycle (the ordering is rebuilt each frame).
            /// </summary>
            private static Entity TabCycleSuccessor(in PhysicsTriggerQueryState queryState,
                ref FixedList64Bytes<Entity> survivors, ref FixedList128Bytes<float> keys)
            {
                if (survivors.Length == 0) return Entity.Null;

                // Insertion sort by (key asc, entity index asc) — stable, deterministic, O(n^2) at the small cap.
                for (var a = 1; a < survivors.Length; a++)
                {
                    var e = survivors[a];
                    var k = keys[a];
                    var b = a - 1;
                    while (b >= 0 && (keys[b] > k || (keys[b] == k && survivors[b].Index > e.Index)))
                    {
                        survivors[b + 1] = survivors[b];
                        keys[b + 1] = keys[b];
                        b--;
                    }

                    survivors[b + 1] = e;
                    keys[b + 1] = k;
                }

                var lastIdx = -1;
                for (var n = 0; n < survivors.Length; n++)
                    if (survivors[n] == queryState.LastWinner)
                    {
                        lastIdx = n;
                        break;
                    }

                // LastWinner gone → head; present → next (wrap).
                var pick = lastIdx < 0 ? 0 : (lastIdx + 1) % survivors.Length;
                return survivors[pick];
            }

            private static bool FlagSet(PhysicsTriggerGateFlags flags, PhysicsTriggerGateFlags f)
            {
                return (flags & f) != 0;
            }
        }

        [BurstCompile]
        private struct ApplyJob : IJob
        {
            [ReadOnly] public NativeStream Events;
            public ComponentLookup<Targets> TargetsLookup;
            public BufferLookup<TriggerQueryHit> HitLookup;
            public ConditionEventWriter.Lookup Writers;

            public void Execute()
            {
                var reader = Events.AsReader();
                for (var i = 0; i < reader.ForEachCount; i++)
                {
                    reader.BeginForEachIndex(i);
                    while (reader.RemainingItemCount > 0)
                    {
                        var evt = reader.Read<TriggerQueryEvent>();

                        if (evt.WriteSlot && TargetsLookup.HasComponent(evt.Routed))
                        {
                            var targets = TargetsLookup[evt.Routed];
                            if (TryWriteSlot(ref targets, evt.Slot, evt.Winner, evt.WriteMode))
                                TargetsLookup[evt.Routed] = targets;
                        }

                        // MirrorIntoWinner — single-threaded cross-entity write (serialization point).
                        if (evt.Self != Entity.Null && evt.Winner != Entity.Null &&
                            TargetsLookup.HasComponent(evt.Winner))
                        {
                            var winnerTargets = TargetsLookup[evt.Winner];
                            if (TryWriteSlot(ref winnerTargets, evt.Slot, evt.Self, evt.WriteMode))
                                TargetsLookup[evt.Winner] = winnerTargets;
                        }

                        if (evt.WriteHit && HitLookup.HasBuffer(evt.Routed))
                        {
                            var hits = HitLookup[evt.Routed];
                            if (evt.ClearHitBuffer) hits.Clear();
                            if (evt.Winner != Entity.Null && hits.Length < 8)
                                hits.Add(new TriggerQueryHit
                                {
                                    Entity = evt.Winner,
                                    Sector = evt.Sector,
                                    Band = evt.Band,
                                    Score = evt.Score
                                });
                        }

                        if (!evt.Condition.Equals(ConditionKey.Null) && Writers.TryGet(evt.Routed, out var writer))
                            writer.Trigger(evt.Condition, evt.Value);
                    }

                    reader.EndForEachIndex();
                }
            }

            private static bool TryWriteSlot(ref Targets targets, PhysicsTriggerRouteSlot slot, Entity winner,
                PhysicsTriggerWriteMode writeMode)
            {
                var current = ReadSlot(in targets, slot);

                switch (writeMode)
                {
                    case PhysicsTriggerWriteMode.SetIfEmpty:
                        if (winner != Entity.Null && current != Entity.Null)
                            return false;
                        break;

                    case PhysicsTriggerWriteMode.ClearOnly:
                        if (winner != Entity.Null)
                            return false;
                        break;
                }

                WriteSlot(ref targets, slot, winner);
                return true;
            }

            private static Entity ReadSlot(in Targets targets, PhysicsTriggerRouteSlot slot)
            {
                return slot switch
                {
                    PhysicsTriggerRouteSlot.Owner => targets.Owner,
                    PhysicsTriggerRouteSlot.Source => targets.Source,
                    PhysicsTriggerRouteSlot.Target => targets.Target,
                    _ => targets.Custom
                };
            }

            private static void WriteSlot(ref Targets targets, PhysicsTriggerRouteSlot slot, Entity value)
            {
                switch (slot)
                {
                    case PhysicsTriggerRouteSlot.Owner:
                        targets.Owner = value;
                        break;
                    case PhysicsTriggerRouteSlot.Source:
                        targets.Source = value;
                        break;
                    case PhysicsTriggerRouteSlot.Target:
                        targets.Target = value;
                        break;
                    default:
                        targets.Custom = value;
                        break;
                }
            }
        }
    }
}
