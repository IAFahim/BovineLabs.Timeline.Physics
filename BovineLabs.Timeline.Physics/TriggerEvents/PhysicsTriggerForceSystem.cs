using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.PIDs;
using BovineLabs.Timeline.Physics.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsPidApplySystem))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
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
        private BufferLookup<PendingExternalForce> _pendingExternalForceLookup;

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
            _pendingExternalForceLookup = state.GetBufferLookup<PendingExternalForce>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerForceData, PhysicsTriggerFilterData, PhysicsClipGate>()
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
            _pendingExternalForceLookup.Update(ref state);

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var configType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerForceData>(true);
            var filterType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true);
            var gateType = SystemAPI.GetComponentTypeHandle<PhysicsClipGate>(true);

            var events = new NativeStream(_query.CalculateChunkCountWithoutFiltering(), state.WorldUpdateAllocator);

            state.Dependency = new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Events = events.AsWriter(),
                TrackBindingTypeHandle = bindingType,
                PhysicsTriggerForceDataTypeHandle = configType,
                PhysicsTriggerFilterDataTypeHandle = filterType,
                PhysicsClipGateTypeHandle = gateType,
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
                PendingForceLookup = _pendingForceLookup,
                PendingExternalForceLookup = _pendingExternalForceLookup
            }.Schedule(state.Dependency);
        }

        private struct TriggerForceEvent
        {
            public Entity Target;
            public PendingForce Force;

            // Impulse hits ride the external (knockback) channel so braking/drag can't eat them; continuous pushes
            // stay on the intent channel where the designer's drag/clamp are meant to shape them.
            public bool External;
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public float DeltaTime;
            public NativeStream.Writer Events;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerForceData> PhysicsTriggerForceDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> PhysicsTriggerFilterDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsClipGate> PhysicsClipGateTypeHandle;

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

                var seenRoots = new NativeHashSet<Entity>(16, Allocator.Temp);

                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref PhysicsTriggerForceDataTypeHandle);
                var filters = chunk.GetNativeArray(ref PhysicsTriggerFilterDataTypeHandle);
                var gates = chunk.GetNativeArray(ref PhysicsClipGateTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var binding = bindings[i];
                    var self = binding.Value;
                    if (self == Entity.Null) continue;
                    if (!LtwLookup.HasComponent(self)) continue;

                    var config = configs[i];
                    var filter = filters[i];
                    var isFirstFrame = gates[i].FirstFrame != 0;
                    var isLastFrame = gates[i].LastFrame != 0;

                    var targets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;

                    var selfPos = PhysicsMath.ResolvePosition(self, in LocalTransformLookup, in LtwLookup,
                        in ParentLookup);
                    var selfRot = PhysicsMath.ResolveRotation(self, in LocalTransformLookup, in LtwLookup,
                        in ParentLookup);

                    seenRoots.Clear();

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (!StatefulEventMatching.Matches(
                                    evt.State, config.EventState, isFirstFrame, isLastFrame) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var otherPos = PhysicsMath.ResolvePosition(evt.EntityB, in LocalTransformLookup,
                                in LtwLookup, in ParentLookup);
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            ProcessEvent(self, evt.EntityB, in config, in filter, midpoint, in targets, selfPos,
                                selfRot, ref seenRoots);
                        }

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                        {
                            if (!StatefulEventMatching.Matches(
                                    evt.State, config.EventState, isFirstFrame, isLastFrame) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var otherPos = PhysicsMath.ResolvePosition(evt.EntityB, in LocalTransformLookup,
                                in LtwLookup, in ParentLookup);
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                            ProcessEvent(self, evt.EntityB, in config, in filter, pt, in targets, selfPos, selfRot,
                                ref seenRoots);
                        }
                }

                seenRoots.Dispose();
                Events.EndForEachIndex();
            }

            [BurstDiscard]
            private static void LogWarning()
            {
                Debug.LogWarning(
                    "PhysicsTriggerForce applied to an entity without a PendingForce buffer. Force ignored.");
            }

            private void ProcessEvent(Entity self, Entity other, in PhysicsTriggerForceData cfg,
                in PhysicsTriggerFilterData filter, float3 contactPoint,
                in Targets targets, float3 selfPos, quaternion selfRot, ref NativeHashSet<Entity> seenRoots)
            {
                if (!PhysicsTriggerFiltering.IsValidTarget(self, other, in filter, in targets, LinkSources, Links))
                    return;

                if (filter.HitMode == PhysicsTriggerHitMode.FirstPerRoot &&
                    !seenRoots.Add(PhysicsTriggerFiltering.ResolveRoot(other, LinkSources)))
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

                var targetPos = PhysicsMath.ResolvePosition(targetToApply, in LocalTransformLookup, in LtwLookup,
                    in ParentLookup);
                var magnitude = cfg.Magnitude * multiplier;

                PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfPos, targetPos, contactPoint,
                    out var origin);
                var offset = targetPos - origin;
                var distSq = math.lengthsq(offset);

                if (cfg.FalloffCurve != PhysicsTriggerFalloffCurve.None && distSq > 1e-5f)
                {
                    var dist = math.sqrt(distSq);
                    var falloff = PhysicsMath.ComputeFalloff(cfg.FalloffCurve, dist, cfg.FalloffStartRadius,
                        cfg.FalloffEndRadius);
                    if (falloff <= 0f) return;

                    magnitude *= falloff;
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
                            force = -offset * math.rsqrt(distSq) * magnitude;
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
                    var isImpulse = cfg.Mode == PhysicsForceMode.Impulse;
                    var timeScale = isImpulse ? 1f : DeltaTime;
                    Events.Write(new TriggerForceEvent
                    {
                        Target = targetToApply,
                        Force = new PendingForce
                        {
                            Linear = force * timeScale,
                            Angular = float3.zero
                        },
                        External = isImpulse
                    });
                }
            }
        }

        [BurstCompile]
        private struct ApplyForcesJob : IJob
        {
            [ReadOnly] public NativeStream Events;
            public BufferLookup<PendingForce> PendingForceLookup;
            public BufferLookup<PendingExternalForce> PendingExternalForceLookup;

            public void Execute()
            {
                var reader = Events.AsReader();
                for (var i = 0; i < reader.ForEachCount; i++)
                {
                    reader.BeginForEachIndex(i);
                    while (reader.RemainingItemCount > 0)
                    {
                        var evt = reader.Read<TriggerForceEvent>();
                        if (evt.External)
                        {
                            if (PendingExternalForceLookup.HasBuffer(evt.Target))
                                PendingExternalForceLookup[evt.Target].Add(new PendingExternalForce
                                    { Linear = evt.Force.Linear, Angular = evt.Force.Angular });
                        }
                        else if (PendingForceLookup.HasBuffer(evt.Target))
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