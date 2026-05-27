using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    public partial struct PhysicsTriggerConditionSystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private ConditionEventWriter.Lookup _writers;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);
            _writers.Create(ref state);

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerConditionData, PhysicsTriggerFilterData, ClipActive>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _writers.Update(ref state);

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var configType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerConditionData>(true);
            var filterType = SystemAPI.GetComponentTypeHandle<PhysicsTriggerFilterData>(true);
            var activePrevType = SystemAPI.GetComponentTypeHandle<ClipActivePrevious>(true);

            state.Dependency = new InvokeJob
            {
                TrackBindingTypeHandle = bindingType,
                PhysicsTriggerConditionDataTypeHandle = configType,
                PhysicsTriggerFilterDataTypeHandle = filterType,
                ClipActivePreviousTypeHandle = activePrevType,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ColliderLookup = _colliderLookup,
                Writers = _writers,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct InvokeJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerConditionData> PhysicsTriggerConditionDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerFilterData> PhysicsTriggerFilterDataTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ClipActivePrevious> ClipActivePreviousTypeHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            public ConditionEventWriter.Lookup Writers;

            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var configs = chunk.GetNativeArray(ref PhysicsTriggerConditionDataTypeHandle);
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

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                            ProcessEvent(self, evt.EntityB, evt.State, in config, in filter, isFirstFrame);

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                        foreach (var evt in collisions)
                            ProcessEvent(self, evt.EntityB, evt.State, in config, in filter, isFirstFrame);
                }
            }

            private void ProcessEvent(Entity self, Entity other, StatefulEventState state,
                in PhysicsTriggerConditionData config, in PhysicsTriggerFilterData filter, bool isFirstFrame)
            {
                if (!StatefulEventMatching.Matches(state, config.EventState, isFirstFrame, false)) return;

                if (config.CollidesWithMask != 0)
                {
                    if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return;
                    if ((collider.Value.Value.GetCollisionFilter().BelongsTo & config.CollidesWithMask) == 0) return;
                }

                // Filter by ignore target and link keys
                var targets = TargetsLookup.HasComponent(self) ? TargetsLookup[self] : default;
                if (!PhysicsTriggerFiltering.IsValidTarget(self, other, in filter, in targets, LinkSources, Links))
                    return;

                if (config.Condition == ConditionKey.Null) return;

                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey, self, other,
                        targets, LinkSources, Links, out var target))
                    if (Writers.TryGet(target, out var writer))
                        writer.Trigger(config.Condition, config.Value);
            }
        }
    }
}