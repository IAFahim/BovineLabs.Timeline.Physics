using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsTriggerTeleportSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> _localTransformLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            _localTransformLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);
            
            state.Dependency = new TeleportJob
            {
                ECB = ecb,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct TeleportJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in PhysicsTriggerTeleportData cfg)
            {
                var self = binding.Value;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    foreach (var evt in triggers)
                        if (evt.State == cfg.EventState)
                            ProcessTeleport(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                    foreach (var evt in collisions)
                        if (evt.State == cfg.EventState)
                            ProcessTeleport(chunkIndex, self, evt.EntityB, cfg,
                                evt.CollisionDetails.AverageContactPointPosition, evt.Normal, true);
            }

            private void ProcessTeleport(int chunkIndex, Entity self, Entity other, in PhysicsTriggerTeleportData cfg,
                float3 contactPoint, float3 contactNormal, bool hasContactData)
            {
                if (!LocalToWorldLookup.TryGetComponent(self, out var selfLtw) ||
                    !LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

                var targetToMove = cfg.EntityToMove switch
                {
                    PhysicsTriggerTargetMode.Self => self,
                    PhysicsTriggerTargetMode.CollidedEntity => other,
                    _ => Entity.Null
                };

                if (cfg.EntityToMove >= PhysicsTriggerTargetMode.ReactionOwner &&
                    TargetsLookup.TryGetComponent(self, out var targets))
                    targetToMove = cfg.EntityToMove switch
                    {
                        PhysicsTriggerTargetMode.ReactionOwner => targets.Owner,
                        PhysicsTriggerTargetMode.ReactionSource => targets.Source,
                        PhysicsTriggerTargetMode.ReactionTarget => targets.Target,
                        _ => targetToMove
                    };

                if (targetToMove == Entity.Null ||
                    !LocalTransformLookup.TryGetComponent(targetToMove, out var targetLt)) return;

                var destPos = cfg.PositionMode switch
                {
                    PhysicsTriggerPositionMode.MatchSelf => selfLtw.Position,
                    PhysicsTriggerPositionMode.MatchCollidedEntity => otherLtw.Position,
                    PhysicsTriggerPositionMode.MatchContactPoint => hasContactData
                        ? contactPoint
                        : (selfLtw.Position + otherLtw.Position) * 0.5f,
                    _ => selfLtw.Position
                };

                var destRot = cfg.RotationMode switch
                {
                    PhysicsTriggerRotationMode.MatchSelf => math.quaternion(selfLtw.Value),
                    PhysicsTriggerRotationMode.MatchCollidedEntity => math.quaternion(otherLtw.Value),
                    PhysicsTriggerRotationMode.AlignToContactNormal => hasContactData
                        ? quaternion.LookRotationSafe(contactNormal, math.up())
                        : quaternion.LookRotationSafe(math.normalize(selfLtw.Position - otherLtw.Position), math.up()),
                    PhysicsTriggerRotationMode.Identity => quaternion.identity,
                    _ => targetLt.Rotation
                };

                if (math.lengthsq(cfg.PositionOffset) > 0)
                    destPos += cfg.IsPositionOffsetLocal
                        ? math.rotate(destRot, cfg.PositionOffset)
                        : cfg.PositionOffset;

                if (math.lengthsq(cfg.RotationOffsetEuler) > 0)
                    destRot = math.mul(destRot, quaternion.Euler(cfg.RotationOffsetEuler));

                targetLt.Position = destPos;
                targetLt.Rotation = destRot;
                ECB.SetComponent(chunkIndex, targetToMove, targetLt);

                if (cfg.ResetVelocity)
                    ECB.SetComponent(chunkIndex, targetToMove,
                        new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            }
        }
    }
}