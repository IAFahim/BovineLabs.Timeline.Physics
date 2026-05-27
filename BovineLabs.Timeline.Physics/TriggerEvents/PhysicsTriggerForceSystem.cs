using BovineLabs.Core;
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

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsTriggerForceSystem))]
    [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
    public partial class PhysicsProducerECBSystem : EntityCommandBufferSystem { }

    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    public partial struct PhysicsTriggerForceSystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private BufferLookup<Stat> _statLookup;
        private BufferLookup<PendingForce> _pendingForceLookup;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _pendingForceLookup = state.GetBufferLookup<PendingForce>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerForceData, PhysicsTriggerFilterData, ClipActive>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _statLookup.Update(ref state);
            _pendingForceLookup.Update(ref state);

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var configType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerForceData>(true);
            var filterType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true);
            var activePrevType = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true);

            var ecbSystem = state.World.GetExistingSystemManaged<PhysicsProducerECBSystem>();
            var ecb = ecbSystem.CreateCommandBuffer();

            state.Dependency = new GatherJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = ecb.AsParallelWriter(),
                TrackBindingTypeHandle = bindingType,
                PhysicsTriggerForceDataTypeHandle = configType,
                PhysicsTriggerFilterDataTypeHandle = filterType,
                ClipActivePreviousTypeHandle = activePrevType,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                LtwLookup = _ltwLookup,
                StatLookup = _statLookup,
                PendingForceLookup = _pendingForceLookup,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct GatherJob : IJobChunk
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter ECB;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerForceData> PhysicsTriggerForceDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> PhysicsTriggerFilterDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [ReadOnly] public BufferLookup<PendingForce> PendingForceLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
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

                    var config = configs[i];
                    var filter = filters[i];
                    var isFirstFrame = !hasActivePrev || !chunk.IsComponentEnabled(ref ClipActivePreviousTypeHandle, i);

                    var targets = TargetsLookup.HasComponent(self) ? TargetsLookup[self] : default;

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, false) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var selfPos = LtwLookup[self].Position;
                            var otherPos = LtwLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            ProcessEvent(unfilteredChunkIndex, self, evt.EntityB, in config, in filter, midpoint, in targets);
                        }

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                        {
                            if (!StatefulEventMatching.Matches(evt.State, config.EventState, isFirstFrame, false) ||
                                !LtwLookup.HasComponent(evt.EntityB)) continue;
                            var selfPos = LtwLookup[self].Position;
                            var otherPos = LtwLookup[evt.EntityB].Position;
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                            ProcessEvent(unfilteredChunkIndex, self, evt.EntityB, in config, in filter, pt, in targets);
                        }
                }
            }

            [BurstDiscard]
            private static void LogWarning()
            {
                UnityEngine.Debug.LogWarning("PhysicsTriggerForce applied to an entity without a PendingForce buffer. Force ignored.");
            }

            private void ProcessEvent(int chunkIndex, Entity self, Entity other, in PhysicsTriggerForceData cfg, in PhysicsTriggerFilterData filter, float3 contactPoint,
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

                var selfLtw = LtwLookup[self];
                var targetLtw = LtwLookup.HasComponent(targetToApply) ? LtwLookup[targetToApply] : LtwLookup[other];
                var magnitude = cfg.Magnitude * multiplier;

                var force = float3.zero;

                switch (cfg.ForceType)
                {
                    case PhysicsTriggerForceType.Directional:
                        force = math.rotate(selfLtw.Rotation, cfg.Direction) * magnitude;
                        break;
                    case PhysicsTriggerForceType.Radial:
                    {
                        PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfLtw, targetLtw, contactPoint,
                            out var origin);
                        var dir = origin - targetLtw.Position;
                        var lenSq = math.lengthsq(dir);
                        if (lenSq > 1e-5f)
                            force = dir / math.sqrt(lenSq) * magnitude;
                        break;
                    }
                    case PhysicsTriggerForceType.Vortex:
                    {
                        PhysicsTriggerResolution.TryResolvePosition(cfg.OriginMode, selfLtw, targetLtw, contactPoint,
                            out var origin);
                        var offset = targetLtw.Position - origin;
                        var up = math.rotate(selfLtw.Rotation, math.up());
                        var projOffset = offset - math.dot(offset, up) * up;
                        var lenSq = math.lengthsq(projOffset);
                        if (lenSq > 1e-5f)
                            force = math.normalize(math.cross(up, projOffset)) * magnitude;
                        break;
                    }
                }

                if (math.lengthsq(force) > 1e-5f)
                {
                    var timeScale = cfg.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;
                    ECB.AppendToBuffer(chunkIndex, targetToApply, new PendingForce
                    {
                        Linear = force * timeScale,
                        Angular = float3.zero
                    });
                }
            }
        }
    }
}