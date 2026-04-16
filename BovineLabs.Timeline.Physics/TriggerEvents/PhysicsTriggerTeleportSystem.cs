// BovineLabs.Timeline.Physics/TriggerEvents/PhysicsTriggerTeleportSystem.cs
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
            this._localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            this._localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            this._targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this._triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            this._collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            this._localTransformLookup.Update(ref state);
            this._localToWorldLookup.Update(ref state);
            this._targetsLookup.Update(ref state);
            this._triggerEventsLookup.Update(ref state);
            this._collisionEventsLookup.Update(ref state);
            
            state.Dependency = new TeleportJob
            {
                ECB = ecb,
                LocalTransformLookup = this._localTransformLookup,
                LocalToWorldLookup = this._localToWorldLookup,
                TargetsLookup = this._targetsLookup,
                TriggerEventsLookup = this._triggerEventsLookup,
                CollisionEventsLookup = this._collisionEventsLookup
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

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerTeleportData cfg)
            {
                var self = binding.Value;

                if (this.TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    var i = 0;
                    while (i < triggers.Length)
                    {
                        var evt = triggers[i];
                        if (evt.State == cfg.EventState)
                        {
                            var midpoint = (this.LocalToWorldLookup[self].Position + this.LocalToWorldLookup[evt.EntityB].Position) * 0.5f;
                            var dir = math.normalizesafe(this.LocalToWorldLookup[self].Position - this.LocalToWorldLookup[evt.EntityB].Position);
                            this.ProcessTeleport(chunkIndex, self, evt.EntityB, cfg, midpoint, dir);
                        }
                        i++;
                    }
                }

                if (this.CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    var i = 0;
                    while (i < collisions.Length)
                    {
                        var evt = collisions[i];
                        if (evt.State == cfg.EventState)
                        {
                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (this.LocalToWorldLookup[self].Position + this.LocalToWorldLookup[evt.EntityB].Position) * 0.5f;
                            var normal = hasContact ? evt.Normal : math.normalizesafe(this.LocalToWorldLookup[self].Position - this.LocalToWorldLookup[evt.EntityB].Position);
                            this.ProcessTeleport(chunkIndex, self, evt.EntityB, cfg, pt, normal);
                        }
                        i++;
                    }
                }
            }

            private void ProcessTeleport(int chunkIndex, Entity self, Entity other, in PhysicsTriggerTeleportData cfg, float3 contactPoint, float3 contactNormal)
            {
                if (!this.LocalToWorldLookup.TryGetComponent(self, out var selfLtw) || !this.LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

                var selfTargets = this.TargetsLookup.TryGetComponent(self, out var t) ? t : default;
                var targetToMove = PhysicsTriggerResolution.ResolveTarget(cfg.EntityToMove, self, other, selfTargets);

                if (targetToMove == Entity.Null || !this.LocalTransformLookup.TryGetComponent(targetToMove, out var targetLt)) return;

                var transform = PhysicsTriggerResolution.CalculateTransform(
                    cfg.PositionMode, cfg.PositionOffset, cfg.IsPositionOffsetLocal,
                    cfg.RotationMode, cfg.RotationOffsetEuler,
                    selfLtw, otherLtw, contactPoint, contactNormal);

                targetLt.Position = transform.Position;
                targetLt.Rotation = transform.Rotation;
                this.ECB.SetComponent(chunkIndex, targetToMove, targetLt);

                if (cfg.ResetVelocity)
                {
                    this.ECB.SetComponent(chunkIndex, targetToMove, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
                }
            }
        }
    }
}