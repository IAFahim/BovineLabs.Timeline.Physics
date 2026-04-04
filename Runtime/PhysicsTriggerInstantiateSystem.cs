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
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecb,
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PhysicsInstantiateJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                {
                    foreach (var evt in triggers)
                    {
                        if (evt.State == cfg.EventState)
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);
                    }
                }

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                {
                    foreach (var evt in collisions)
                    {
                        if (evt.State == cfg.EventState)
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, evt.CollisionDetails.AverageContactPointPosition, evt.Normal, true);
                    }
                }
            }

            private void ProcessSpawn(int chunkIndex, Entity self, Entity other, in PhysicsTriggerInstantiateData cfg, float3 contactPoint, float3 contactNormal, bool hasContactData)
            {
                if (!LocalToWorldLookup.TryGetComponent(self, out var selfLtw)) return;
                if (!LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

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

                var instance = ECB.Instantiate(chunkIndex, cfg.Prefab);

                ECB.SetComponent(chunkIndex, instance, LocalTransform.FromPositionRotation(spawnPos, spawnRot));

                if (cfg.ParentMode != InstantiateParentMode.None)
                {
                    Entity parentEntity = cfg.ParentMode switch
                    {
                        InstantiateParentMode.ParentToCollidedEntity => other,
                        _ => Entity.Null
                    };

                    if (cfg.ParentMode >= InstantiateParentMode.ParentToReactionOwner && TargetsLookup.TryGetComponent(self, out var targets))
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
                        ECB.AddComponent(chunkIndex, instance, new Parent { Value = parentEntity });

                        if (LocalToWorldLookup.TryGetComponent(parentEntity, out var newParentLtw))
                        {
                            var worldMatrix = float4x4.TRS(spawnPos, spawnRot, 1f);
                            var localMatrix = math.mul(math.inverse(newParentLtw.Value), worldMatrix);
                            
                            localMatrix.ExtractLocalTransform(out var localTransformFinal);
                            ECB.SetComponent(chunkIndex, instance, localTransformFinal);
                        }
                    }
                }

                if (TargetsLookup.TryGetComponent(self, out var selfTargets) && TargetsLookup.TryGetComponent(other, out var otherTargets))
                {
                    ECB.SetComponent(chunkIndex, instance, new Targets
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