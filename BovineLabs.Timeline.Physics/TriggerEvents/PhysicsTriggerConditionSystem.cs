using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct PhysicsTriggerConditionSystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomLookup;
        private ComponentLookup<EntityLinkSource> _linkSourceLookup;
        private BufferLookup<EntityLinkEntry> _linkLookup;
        private ComponentLookup<PhysicsCollider> _colliderLookup;
        private ConditionEventWriter.Lookup _writers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            _linkSourceLookup = state.GetComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetBufferLookup<EntityLinkEntry>(true);
            _colliderLookup = state.GetComponentLookup<PhysicsCollider>(true);
            _writers.Create(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _targetsCustomLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _writers.Update(ref state);

            state.Dependency = new InvokeJob
            {
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                ColliderLookup = _colliderLookup,
                Writers = _writers,
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct InvokeJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public ComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public BufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public ComponentLookup<PhysicsCollider> ColliderLookup;
            public ConditionEventWriter.Lookup Writers;

            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute(in TrackBinding binding, in PhysicsTriggerConditionData config)
            {
                var self = binding.Value;
                if (self == Entity.Null) return;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    foreach (var evt in triggers)
                        ProcessEvent(self, evt.EntityB, evt.State, in config);

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                    foreach (var evt in collisions)
                        ProcessEvent(self, evt.EntityB, evt.State, in config);
            }

            private void ProcessEvent(Entity self, Entity other, StatefulEventState state,
                in PhysicsTriggerConditionData config)
            {
                if (state != config.EventState) return;

                if (config.CollidesWithMask != 0)
                {
                    if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return;
                    if ((collider.Value.Value.GetCollisionFilter().BelongsTo & config.CollidesWithMask) == 0) return;
                }

                if (config.Condition == ConditionKey.Null) return;

                var targets = TargetsLookup.HasComponent(self) ? TargetsLookup[self] : default;

                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RouteTo, config.RouteLinkKey, self, other,
                        targets, TargetsCustomLookup, LinkSources, Links, out var target))
                    if (Writers.TryGet(target, out var writer))
                        writer.Trigger(config.Condition, config.Value);
            }
        }
    }
}