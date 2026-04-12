using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
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
    public partial struct PhysicsTriggerInstantiateSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<LocalToWorld> localToWorldLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            this.localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            this.localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            this.triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            this.collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this.localTransformLookup.Update(ref state);
            this.localToWorldLookup.Update(ref state);
            this.targetsLookup.Update(ref state);
            this.triggerEventsLookup.Update(ref state);
            this.collisionEventsLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecb,
                LocalTransformLookup = this.localTransformLookup,
                LocalToWorldLookup = this.localToWorldLookup,
                TargetsLookup = this.targetsLookup,
                TriggerEventsLookup = this.triggerEventsLookup,
                CollisionEventsLookup = this.collisionEventsLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;

                if (this.TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    for (int i = 0; i < triggers.Length; i++)
                    {
                        var evt = triggers[i];
                        if (evt.State == cfg.EventState)
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);
                    }
                }

                if (this.CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    for (int i = 0; i < collisions.Length; i++)
                    {
                        var evt = collisions[i];
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
                    InstantiateRotationMode.Identity => quaternion.identity,
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

                var instance = this.ECB.Instantiate(chunkIndex, cfg.Prefab);

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