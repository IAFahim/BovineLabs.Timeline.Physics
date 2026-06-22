using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.Teleports;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    /// <summary>
    ///     Runs before the other trigger reactions and the kinematics producer so a winner selected
    ///     this tick is consumable by tracks targeting <see cref="Target.Custom" /> in the same tick.
    ///     Line of sight is evaluated against the previous step's collision world, matching the
    ///     ricochet and teleport tracks. When no physics world exists, the line-of-sight gate is
    ///     treated as passing.
    /// </summary>
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
        private UnsafeComponentLookup<Targets> _targetsReadLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private ComponentLookup<Targets> _targetsWriteLookup;
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
            _targetsWriteLookup = state.GetComponentLookup<Targets>();
            _writers.Create(ref state);

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsTriggerQueryState>()
                .WithAll<TrackBinding, PhysicsTriggerQueryData, PhysicsTriggerFilterData, ClipActive>()
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
            _targetsWriteLookup.Update(ref state);
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
                ClipActivePreviousTypeHandle = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true),
                TimerDataTypeHandle = SystemAPI.GetComponentTypeHandle<TimerData>(true),
                TimeTransformTypeHandle = SystemAPI.GetComponentTypeHandle<TimeTransform>(true),
                TargetsLookup = _targetsReadLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ColliderLookup = _colliderLookup,
                LtwLookup = _ltwLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true),
                HasCollisionWorld = hasCollisionWorld,
                CollisionWorld = hasCollisionWorld ? physicsWorld.CollisionWorld : default
            }.ScheduleParallel(_query, state.Dependency);

            state.Dependency = new ApplyJob
            {
                Events = events,
                TargetsLookup = _targetsWriteLookup,
                Writers = _writers
            }.Schedule(state.Dependency);
        }

        /// <summary>
        ///     Produced by <see cref="GatherJob" />, consumed in deterministic chunk order by
        ///     <see cref="ApplyJob" />, mirroring the trigger force system's pattern.
        /// </summary>
        private struct TriggerQueryEvent
        {
            public Entity Routed;
            public Entity Winner;
            public bool WriteCustom;
            public ConditionKey Condition;
            public int Value;
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public NativeStream.Writer Events;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerQueryData> QueryDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> FilterDataTypeHandle;
            public ComponentTypeHandle<PhysicsTriggerQueryState> QueryStateTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimerData> TimerDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimeTransform> TimeTransformTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public bool HasCollisionWorld;
            [ReadOnly] public CollisionWorld CollisionWorld;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Events.BeginForEachIndex(unfilteredChunkIndex);

                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref QueryDataTypeHandle);
                var filters = chunk.GetNativeArray(ref FilterDataTypeHandle);
                var states = chunk.GetNativeArray(ref QueryStateTypeHandle);

                var hasActivePrev = chunk.Has(ref ClipActivePreviousTypeHandle);
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

                    var isFirstFrame = !hasActivePrev ||
                                       !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);
                    if (isFirstFrame) queryState = default;

                    var isLastFrame = false;
                    if (hasTiming)
                        isLastFrame = StatefulEventMatching.IsClipLastFrame(timers[i], timeTransforms[i]);

                    if (!PhysicsMath.TryResolveTransform(self, in LocalTransformLookup, in LtwLookup,
                            in ParentLookup, out var selfPos, out var selfRot))
                        continue;

                    var forward = math.rotate(selfRot, math.forward());
                    if (!math.all(math.isfinite(forward))) continue;

                    var maxDistSq = config.MaxDistance > 0f ? config.MaxDistance * config.MaxDistance
                        : float.MaxValue;
                    var minAlignment = config.MaxAngle > 0f ? math.cos(config.MaxAngle) : float.MinValue;
                    var targets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;

                    var best = new Candidate { Entity = Entity.Null };

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                            Consider(self, evt.EntityB, evt.State, in config, in filter, in targets, selfPos,
                                forward, maxDistSq, minAlignment, isFirstFrame, isLastFrame, ref best);

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                            Consider(self, evt.EntityB, evt.State, in config, in filter, in targets, selfPos,
                                forward, maxDistSq, minAlignment, isFirstFrame, isLastFrame, ref best);

                    var winner = best.Entity;
                    if (winner != Entity.Null && winner != queryState.LastWinner)
                    {
                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey,
                                self, winner, targets, LinkSources, Links, out var routed))
                            Events.Write(new TriggerQueryEvent
                            {
                                Routed = routed,
                                Winner = winner,
                                WriteCustom = true,
                                Condition = config.FoundCondition,
                                Value = config.FoundValue
                            });
                    }
                    else if (winner == Entity.Null && queryState.LastWinner != Entity.Null)
                    {
                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey,
                                self, queryState.LastWinner, targets, LinkSources, Links, out var routed))
                            Events.Write(new TriggerQueryEvent
                            {
                                Routed = routed,
                                Winner = Entity.Null,
                                WriteCustom = config.ClearOnLost,
                                Condition = config.LostCondition,
                                Value = config.LostValue
                            });
                    }

                    if (winner != queryState.LastWinner)
                    {
                        queryState.LastWinner = winner;
                        states[i] = queryState;
                    }
                    else if (isFirstFrame)
                    {
                        states[i] = queryState;
                    }
                }

                Events.EndForEachIndex();
            }

            private struct Candidate
            {
                public Entity Entity;
                public float Score;
            }

            private void Consider(Entity self, Entity other, StatefulEventState eventState,
                in PhysicsTriggerQueryData config, in PhysicsTriggerFilterData filter, in Targets targets,
                float3 selfPos, float3 forward, float maxDistSq, float minAlignment, bool isFirstFrame,
                bool isLastFrame, ref Candidate best)
            {
                if (other == Entity.Null) return;
                if (!StatefulEventMatching.Matches(eventState, config.EventState, isFirstFrame, isLastFrame))
                    return;

                if (config.CollidesWithMask != 0)
                {
                    if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return;
                    if ((collider.Value.Value.GetCollisionFilter().BelongsTo & config.CollidesWithMask) == 0)
                        return;
                }

                if (!PhysicsTriggerFiltering.IsValidTarget(self, other, in filter, in targets, LinkSources, Links))
                    return;

                if (!LtwLookup.TryGetComponent(other, out var otherLtw)) return;

                var offset = otherLtw.Position - selfPos;
                var distSq = math.lengthsq(offset);
                if (distSq > maxDistSq) return;

                var alignment = distSq > 1e-8f ? math.dot(forward, offset * math.rsqrt(distSq)) : 1f;
                if (alignment < minAlignment) return;

                if (config.RequireLineOfSight && HasCollisionWorld &&
                    !TeleportMath.CheckLineOfSight(in CollisionWorld, selfPos, otherLtw.Position,
                        config.LineOfSightOffset, config.ObstacleMask, self, other))
                    return;

                var score = config.Selection switch
                {
                    PhysicsTriggerQuerySelection.Nearest => -distSq,
                    PhysicsTriggerQuerySelection.Farthest => distSq,
                    PhysicsTriggerQuerySelection.MostAligned => alignment,
                    PhysicsTriggerQuerySelection.LeastAligned => -alignment,
                    _ => -distSq
                };

                if (best.Entity == Entity.Null ||
                    score > best.Score ||
                    (score == best.Score && IsLowerEntity(other, best.Entity)))
                    best = new Candidate { Entity = other, Score = score };
            }

            private static bool IsLowerEntity(Entity a, Entity b)
            {
                return a.Index != b.Index ? a.Index < b.Index : a.Version < b.Version;
            }
        }

        /// <summary>
        ///     Runs single-threaded so writes to shared <see cref="Targets" /> components and condition
        ///     buffers stay race-free regardless of how many query clips routed to the same entity.
        /// </summary>
        [BurstCompile]
        private struct ApplyJob : IJob
        {
            [ReadOnly] public NativeStream Events;
            public ComponentLookup<Targets> TargetsLookup;
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

                        if (evt.WriteCustom && TargetsLookup.HasComponent(evt.Routed))
                        {
                            var targets = TargetsLookup[evt.Routed];
                            targets.Custom = evt.Winner;
                            TargetsLookup[evt.Routed] = targets;
                        }

                        if (!evt.Condition.Equals(ConditionKey.Null) && Writers.TryGet(evt.Routed, out var writer))
                            writer.Trigger(evt.Condition, evt.Value);
                    }

                    reader.EndForEachIndex();
                }
            }
        }
    }
}
