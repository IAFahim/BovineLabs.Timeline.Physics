using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> _localTransformLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ObjectDefinitionRegistry>();
            this._localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            this._localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            this._targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this._triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            this._collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this._localTransformLookup.Update(ref state);
            this._localToWorldLookup.Update(ref state);
            this._targetsLookup.Update(ref state);
            this._triggerEventsLookup.Update(ref state);
            this._collisionEventsLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecb,
                ObjectDefinitionRegistry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                LocalToWorldLookup = this._localToWorldLookup,
                TargetsLookup = this._targetsLookup,
                TriggerEventsLookup = this._triggerEventsLookup,
                CollisionEventsLookup = this._collisionEventsLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            [WriteOnly] public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ObjectDefinitionRegistry ObjectDefinitionRegistry;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;

                if (this.TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    foreach (var evt in triggers)
                    {
                        if (evt.State == cfg.EventState)
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);
                    }
                }

                if (this.CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    foreach (var evt in collisions)
                    {
                        if (evt.State == cfg.EventState)
                        {
                            var isValid = evt.TryGetDetails(out var details);
                            var pt = isValid ? details.AverageContactPointPosition : float3.zero;
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, pt, evt.Normal, isValid);
                        }
                    }
                }
            }

            private void ProcessSpawn(int chunkIndex, Entity self, Entity other, in PhysicsTriggerInstantiateData cfg, float3 contactPoint, float3 contactNormal, bool hasContactData)
            {
                if (!this.LocalToWorldLookup.TryGetComponent(self, out var selfLtw)) return;
                if (!this.LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

                float3 spawnPos = cfg.PositionMode switch
                {
                    InstantiatePositionMode.MatchSelf => selfLtw.Position,
                    InstantiatePositionMode.MatchCollidedEntity => otherLtw.Position,
                    InstantiatePositionMode.MatchContactPoint => hasContactData ? contactPoint : (selfLtw.Position + otherLtw.Position) * 0.5f,
                    _ => selfLtw.Position
                };

                quaternion spawnRot = cfg.RotationMode switch
                {
                    InstantiateRotationMode.MatchSelf => math.quaternion(selfLtw.Value),
                    InstantiateRotationMode.MatchCollidedEntity => math.quaternion(otherLtw.Value),
                    InstantiateRotationMode.AlignToContactNormal => hasContactData 
                        ? quaternion.LookRotationSafe(contactNormal, math.up()) 
                        : quaternion.LookRotationSafe(math.normalize(selfLtw.Position - otherLtw.Position), math.up()),
                    _ => quaternion.identity
                };

                if (math.lengthsq(cfg.PositionOffset) > 0)
                {
                    spawnPos += cfg.IsPositionOffsetLocal ? math.rotate(spawnRot, cfg.PositionOffset) : cfg.PositionOffset;
                }

                if (math.lengthsq(cfg.RotationOffsetEuler) > 0)
                {
                    spawnRot = math.mul(spawnRot, quaternion.Euler(cfg.RotationOffsetEuler));
                }

                var instance = this.ECB.Instantiate(chunkIndex, ObjectDefinitionRegistry[cfg.ObjectId]);

                this.ECB.SetComponent(chunkIndex, instance, LocalTransform.FromPositionRotation(spawnPos, spawnRot));

                if (cfg.ParentMode != InstantiateParentMode.None)
                {
                    Entity parentEntity = cfg.ParentMode switch
                    {
                        InstantiateParentMode.ParentToCollidedEntity => other,
                        _ => Entity.Null
                    };

                    if (cfg.ParentMode >= InstantiateParentMode.ParentToReactionOwner && this.TargetsLookup.TryGetComponent(self, out var targets))
                    {
                        parentEntity = cfg.ParentMode switch
                        {
                            InstantiateParentMode.ParentToReactionOwner => targets.Owner,
                            InstantiateParentMode.ParentToReactionSource => targets.Source,
                            InstantiateParentMode.ParentToReactionTarget => targets.Target,
                            _ => parentEntity
                        };
                    }

                    if (parentEntity != Entity.Null)
                    {
                        this.ECB.AddComponent(chunkIndex, instance, new Parent { Value = parentEntity });

                        if (this.LocalToWorldLookup.TryGetComponent(parentEntity, out var newParentLtw))
                        {
                            var worldMatrix = float4x4.TRS(spawnPos, spawnRot, 1f);
                            var localMatrix = math.mul(math.inverse(newParentLtw.Value), worldMatrix);
                            
                            localMatrix.ExtractLocalTransform(out var localTransformFinal);
                            this.ECB.SetComponent(chunkIndex, instance, localTransformFinal);
                        }
                    }
                }

                if (this.TargetsLookup.TryGetComponent(self, out var selfTargets) && this.TargetsLookup.TryGetComponent(other, out var otherTargets))
                {
                    this.ECB.SetComponent(chunkIndex, instance, new Targets
                    {
                        Owner = selfTargets.Owner,
                        Source = selfTargets.Source,
                        Target = otherTargets.Source
                    });
                }
            }
        }
    }
}