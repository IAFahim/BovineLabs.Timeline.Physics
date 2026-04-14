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
            _localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localTransformLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            state.Dependency = new PhysicsInstantiateJob
            {
                ECB = ecb,
                ObjectDefinitionRegistry = SystemAPI.GetSingleton<ObjectDefinitionRegistry>(),
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
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

            private void Execute([ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in PhysicsTriggerInstantiateData cfg)
            {
                var self = binding.Value;

                if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    foreach (var evt in triggers)
                        if (evt.State == cfg.EventState)
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, float3.zero, float3.zero, false);

                if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                    foreach (var evt in collisions)
                        if (evt.State == cfg.EventState)
                        {
                            var isValid = evt.TryGetDetails(out var details);
                            var pt = isValid ? details.AverageContactPointPosition : float3.zero;
                            ProcessSpawn(chunkIndex, self, evt.EntityB, cfg, pt, evt.Normal, isValid);
                        }
            }

            private void ProcessSpawn(int chunkIndex, Entity self, Entity other, in PhysicsTriggerInstantiateData cfg,
                float3 contactPoint, float3 contactNormal, bool hasContactData)
            {
                if (!LocalToWorldLookup.TryGetComponent(self, out var selfLtw)) return;
                if (!LocalToWorldLookup.TryGetComponent(other, out var otherLtw)) return;

                var spawnPos = cfg.PositionMode switch
                {
                    InstantiatePositionMode.MatchSelf => selfLtw.Position,
                    InstantiatePositionMode.MatchCollidedEntity => otherLtw.Position,
                    InstantiatePositionMode.MatchContactPoint => hasContactData
                        ? contactPoint
                        : (selfLtw.Position + otherLtw.Position) * 0.5f,
                    _ => selfLtw.Position
                };

                var spawnRot = cfg.RotationMode switch
                {
                    InstantiateRotationMode.MatchSelf => math.quaternion(selfLtw.Value),
                    InstantiateRotationMode.MatchCollidedEntity => math.quaternion(otherLtw.Value),
                    InstantiateRotationMode.AlignToContactNormal => hasContactData
                        ? quaternion.LookRotationSafe(contactNormal, math.up())
                        : quaternion.LookRotationSafe(math.normalize(selfLtw.Position - otherLtw.Position), math.up()),
                    _ => quaternion.identity
                };

                if (math.lengthsq(cfg.PositionOffset) > 0)
                    spawnPos += cfg.IsPositionOffsetLocal
                        ? math.rotate(spawnRot, cfg.PositionOffset)
                        : cfg.PositionOffset;

                if (math.lengthsq(cfg.RotationOffsetEuler) > 0)
                    spawnRot = math.mul(spawnRot, quaternion.Euler(cfg.RotationOffsetEuler));

                var instance = ECB.Instantiate(chunkIndex, ObjectDefinitionRegistry[cfg.ObjectId]);

                ECB.SetComponent(chunkIndex, instance, LocalTransform.FromPositionRotation(spawnPos, spawnRot));

                if (cfg.ParentMode != InstantiateParentMode.None)
                {
                    var parentEntity = cfg.ParentMode switch
                    {
                        InstantiateParentMode.ParentToCollidedEntity => other,
                        _ => Entity.Null
                    };

                    if (cfg.ParentMode >= InstantiateParentMode.ParentToReactionOwner &&
                        TargetsLookup.TryGetComponent(self, out var targets))
                        parentEntity = cfg.ParentMode switch
                        {
                            InstantiateParentMode.ParentToReactionOwner => targets.Owner,
                            InstantiateParentMode.ParentToReactionSource => targets.Source,
                            InstantiateParentMode.ParentToReactionTarget => targets.Target,
                            _ => parentEntity
                        };

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

                if (TargetsLookup.TryGetComponent(self, out var selfTargets) &&
                    TargetsLookup.TryGetComponent(other, out var otherTargets))
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