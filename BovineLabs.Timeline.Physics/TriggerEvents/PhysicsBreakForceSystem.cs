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
using BovineLabs.Timeline.Physics.Stats;
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
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsForceTrackSystem))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsBreakForceSystem : ISystem
    {
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
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
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _pendingForceLookup = state.GetBufferLookup<PendingForce>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsBreakForceData, PhysicsTriggerFilterData, ClipActive>()
                .Build();

            state.RequireForUpdate(_query);
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
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);
            _statLookup.Update(ref state);
            _pendingForceLookup.Update(ref state);

            var events = new NativeStream(_query.CalculateChunkCountWithoutFiltering(), state.WorldUpdateAllocator);

            state.Dependency = new GatherJob
            {
                Events = events.AsWriter(),
                TrackBindingTypeHandle = SystemAPI.GetComponentTypeHandle<TrackBinding>(true),
                BreakDataTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsBreakForceData>(true),
                FilterTypeHandle = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true),
                ClipActivePreviousTypeHandle = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true),
                TimerDataTypeHandle = SystemAPI.GetComponentTypeHandle<TimerData>(true),
                TimeTransformTypeHandle = SystemAPI.GetComponentTypeHandle<TimeTransform>(true),
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                LtwLookup = _ltwLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                VelocityLookup = _velocityLookup,
                MassLookup = _massLookup,
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

        private struct BreakForceEvent
        {
            public Entity Target;
            public PendingForce Force;
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public NativeStream.Writer Events;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsBreakForceData> BreakDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> FilterTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimerData> TimerDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TimeTransform> TimeTransformTypeHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;
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
                var configs = chunk.GetNativeArray(ref BreakDataTypeHandle);
                var filters = chunk.GetNativeArray(ref FilterTypeHandle);

                var hasActivePrev = chunk.Has(ref ClipActivePreviousTypeHandle);
                var hasTiming = chunk.Has(ref TimerDataTypeHandle) && chunk.Has(ref TimeTransformTypeHandle);
                var timers = hasTiming ? chunk.GetNativeArray(ref TimerDataTypeHandle) : default;
                var timeTransforms = hasTiming ? chunk.GetNativeArray(ref TimeTransformTypeHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var self = bindings[i].Value;
                    if (self == Entity.Null || !LtwLookup.HasComponent(self)) continue;

                    var config = configs[i];
                    var filter = filters[i];
                    var isFirstFrame = !hasActivePrev || !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);
                    var isLastFrame = false;
                    if (hasTiming)
                    {
                        var timer = timers[i];
                        var timeTransform = timeTransforms[i];
                        isLastFrame = StatefulEventMatching.IsClipLastFrame(in timer, in timeTransform);
                    }

                    var targets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;
                    var selfRot = PhysicsMath.ResolveRotation(self, in LocalTransformLookup, in LtwLookup,
                        in ParentLookup);

                    seenRoots.Clear();

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                            if (StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, isLastFrame))
                                ProcessEvent(self, evt.EntityB, in config, in filter, in targets, selfRot,
                                    ref seenRoots);

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                            if (StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, isLastFrame))
                                ProcessEvent(self, evt.EntityB, in config, in filter, in targets, selfRot,
                                    ref seenRoots);
                }

                seenRoots.Dispose();
                Events.EndForEachIndex();
            }

            private void ProcessEvent(Entity self, Entity other, in PhysicsBreakForceData cfg,
                in PhysicsTriggerFilterData filter, in Targets targets, quaternion selfRot,
                ref NativeHashSet<Entity> seenRoots)
            {
                if (other == Entity.Null || !LtwLookup.HasComponent(other)) return;

                if (!PhysicsTriggerFiltering.IsValidTarget(self, other, in filter, in targets, LinkSources, Links))
                    return;

                if (!PhysicsTriggerResolution.TryResolveLinkedTarget(cfg.ApplyTo, cfg.ApplyToLinkKey, self, other,
                        targets, LinkSources, Links, out var victim))
                    return;

                if (!seenRoots.Add(victim)) return;

                if (!PendingForceLookup.HasBuffer(victim) ||
                    !VelocityLookup.TryGetComponent(victim, out var pv)) return;

                var mass = MassLookup.TryGetComponent(victim, out var pm) && pm.InverseMass > 1e-6f
                    ? 1f / pm.InverseMass
                    : 0f;
                if (mass <= 0f) return;

                var v = pv.Linear;
                var speed = math.length(v);
                if (speed < 1e-4f) return;

                var multiplier =
                    StatStrengthUtility.Resolve(in cfg.Strength, self, targets, LinkSources, Links, StatLookup);
                var threshold = cfg.BaseThreshold * multiplier;

                if (threshold > 0f && speed * mass > threshold) return;

                var deltaV = BreakImpulseKernel.ComputeDeltaV(cfg.Mode, selfRot, v, speed, cfg.Elevation,
                    cfg.Azimuth, cfg.Restitution);

                var impulse = deltaV * mass;
                if (math.lengthsq(impulse) > 1e-5f)
                    Events.Write(new BreakForceEvent
                    {
                        Target = victim,
                        Force = new PendingForce { Linear = impulse, Angular = float3.zero }
                    });
            }
        }

        [BurstCompile]
        private struct ApplyForcesJob : IJob
        {
            [ReadOnly] public NativeStream Events;
            public BufferLookup<PendingForce> PendingForceLookup;

            public void Execute()
            {
                var reader = Events.AsReader();
                for (var i = 0; i < reader.ForEachCount; i++)
                {
                    reader.BeginForEachIndex(i);
                    while (reader.RemainingItemCount > 0)
                    {
                        var evt = reader.Read<BreakForceEvent>();
                        if (PendingForceLookup.HasBuffer(evt.Target)) PendingForceLookup[evt.Target].Add(evt.Force);
                    }

                    reader.EndForEachIndex();
                }
            }
        }
    }
}