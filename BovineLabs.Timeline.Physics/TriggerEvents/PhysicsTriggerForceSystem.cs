using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsPidApplySystem))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsTriggerForceSystem : ISystem
    {
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private BufferLookup<Stat> _statLookup;
        private BufferLookup<PendingForce> _pendingForceLookup;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _pendingForceLookup = state.GetBufferLookup<PendingForce>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerForceData, PhysicsTriggerFilterData, ClipActive>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _statLookup.Update(ref state);
            _pendingForceLookup.Update(ref state);

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var configType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerForceData>(true);
            var filterType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true);
            var activePrevType = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true);

            var events = new NativeStream(_query.CalculateChunkCountWithoutFiltering(), state.WorldUpdateAllocator);

            state.Dependency = new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Events = events.AsWriter(),
                TrackBindingTypeHandle = bindingType,
                PhysicsTriggerForceDataTypeHandle = configType,
                PhysicsTriggerFilterDataTypeHandle = filterType,
                ClipActivePreviousTypeHandle = activePrevType,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                LtwLookup = _ltwLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                StatLookup = _statLookup,
                PendingForceLookup = _pendingForceLookup,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);

            state.Dependency = new ApplyForcesJob
            {
                Events = events,
                PendingForceLookup = _pendingForceLookup
            }.Schedule(state.Dependency);
        }

        /// <summary>
        /// Intermediate event produced by <see cref="GatherJob"/> and consumed by <see cref="ApplyForcesJob"/>.
        /// Stored in a <see cref="NativeStream"/> so that buffer appends happen in a deterministic order
        /// without requiring a managed ECB system.
        /// </summary>
        private struct TriggerForceEvent
        {
            public Entity Target;
            public PendingForce Force;
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public float DeltaTime;
            public NativeStream.Writer Events;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerForceData> PhysicsTriggerForceDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> PhysicsTriggerFilterDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [ReadOnly] public BufferLookup<PendingForce> PendingForceLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                Events.BeginForEachIndex(unfilteredChunkIndex);

                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref PhysicsTriggerForceDataTypeHandle);
                var filters = chunk.GetNativeArray(ref PhysicsTriggerFilterDataTypeHandle);

                var hasActivePrev = chunk.Has(ref ClipActivePreviousTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var binding = bindings[i];
                    var self = binding.Value;
                    if (self == Entity.Null) continue;
                    if (!LtwLookup.HasComponent(self)) continue;

                    var config = configs[i];
                    var filter = filters[i];
                    var isFirstFrame = !hasActivePrev || !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);

                    var targets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, false) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var selfPos = PhysicsMath.ResolvePosition(self, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                            var otherPos = PhysicsMath.ResolvePosition(evt.EntityB, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            ProcessEvent(self, evt.EntityB, in config, in filter, midpoint, in targets);
                        }

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, false) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var selfPos = PhysicsMath.ResolvePosition(self, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                            var otherPos = PhysicsMath.ResolvePosition(evt.EntityB, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                            ProcessEvent(self, evt.EntityB, in config, in filter, pt, in targets);
                        }
                }
                
                Events.EndForEachIndex();
            }

            [BurstDiscard]
            private static void LogWarning()
            {
                Debug.LogWarning("PhysicsTriggerForce applied to an entity without a PendingForce buffer. Force ignored.");
            }

            private void ProcessEvent(Entity self, Entity other, in PhysicsTriggerForceData cfg, in PhysicsTriggerFilterData filter, float3 contactPoint,
                in Targets targets)
            {
                if (!PhysicsTriggerFiltering.IsValidTarget(self, other, in filter, in targets, LinkSources, Links))
                    return;

                if (!PhysicsTriggerResolution.TryResolveLinkedTarget(
                        cfg.ApplyTo, cfg.ApplyToLinkKey, self, other, targets, LinkSources,
                        Links, out var targetToApply)) return;

                if (!PendingForceLookup.HasBuffer(targetToApply))
                {
                    LogWarning();
                    return;
                }

                var multiplier =
                    StatStrengthUtility.Resolve(in cfg.Strength, self, targets, LinkSources, Links, StatLookup);

                if (math.abs(multiplier) < 1e-5f || math.abs(cfg.Magnitude) < 1e-5f) return;

                var selfPos = PhysicsMath.ResolvePosition(self, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                var selfRot = PhysicsMath.ResolveRotation(self, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                var targetPos = PhysicsMath.ResolvePosition(targetToApply, in LocalTransformLookup, in LtwLookup, in ParentLookup);
                var magnitude = cfg.Magnitude * multiplier;

                PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfPos, targetPos, contactPoint, out var origin);
                var offset = targetPos - origin;
                var distSq = math.lengthsq(offset);

                if (cfg.FalloffCurve != PhysicsTriggerFalloffCurve.None && distSq > 1e-5f)
                {
                    var dist = math.sqrt(distSq);
                    if (dist > cfg.FalloffEndRadius) return;
                    
                    if (dist > cfg.FalloffStartRadius)
                    {
                        // For Step curve, we maintain 100% magnitude until EndRadius, where it is cut off by the check above.
                        

                        var range = math.max(cfg.FalloffEndRadius - cfg.FalloffStartRadius, 0.001f);
                        var factor = math.clamp(1f - ((dist - cfg.FalloffStartRadius) / range), 0f, 1f);
                        
                        if (cfg.FalloffCurve == PhysicsTriggerFalloffCurve.Linear)
                            magnitude *= factor;
                        else if (cfg.FalloffCurve == PhysicsTriggerFalloffCurve.InverseSquare)
                            magnitude *= factor * factor;
                    }
                }

                if (math.abs(magnitude) < 1e-5f) return;

                var force = float3.zero;

                switch (cfg.ForceType)
                {
                    case PhysicsTriggerForceType.Directional:
                        force = math.rotate(selfRot, cfg.Direction) * magnitude;
                        break;
                    case PhysicsTriggerForceType.Radial:
                    {
                        if (distSq > 1e-5f)
                            force = -offset / math.sqrt(distSq) * magnitude;
                        break;
                    }
                    case PhysicsTriggerForceType.Vortex:
                    {
                        var up = math.rotate(selfRot, math.up());
                        var projOffset = offset - math.dot(offset, up) * up;
                        var projSq = math.lengthsq(projOffset);
                        if (projSq > 1e-5f)
                            force = math.normalize(math.cross(up, projOffset)) * magnitude;
                        break;
                    }
                }

                if (math.lengthsq(force) > 1e-5f)
                {
                    var timeScale = cfg.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;
                    Events.Write(new TriggerForceEvent
                    {
                        Target = targetToApply,
                        Force = new PendingForce
                        {
                            Linear = force * timeScale,
                            Angular = float3.zero
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Applies gathered force events to their target entity's <see cref="PendingForce"/> buffer.
        /// Runs single-threaded to avoid parallel <see cref="BufferLookup{T}"/> write conflicts when
        /// multiple events target the same entity.
        /// </summary>
        [BurstCompile]
        private struct ApplyForcesJob : IJob
        {
            [ReadOnly] public NativeStream Events;
            public BufferLookup<PendingForce> PendingForceLookup;

            public void Execute()
            {
                var reader = Events.AsReader();
                for (int i = 0; i < reader.ForEachCount; i++)
                {
                    reader.BeginForEachIndex(i);
                    while (reader.RemainingItemCount > 0)
                    {
                        var evt = reader.Read<TriggerForceEvent>();
                        if (PendingForceLookup.HasBuffer(evt.Target))
                        {
                            PendingForceLookup[evt.Target].Add(evt.Force);
                        }
                    }
                    reader.EndForEachIndex();
                }
            }
        }
    }
}