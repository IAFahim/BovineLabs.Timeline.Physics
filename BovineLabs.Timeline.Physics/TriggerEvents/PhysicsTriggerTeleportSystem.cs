using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Core.Utility;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsTriggerTeleportSystem : ISystem
    {
        private EntityQuery query;
        private EntityLock entityLock;

        private EntityTypeHandle entityHandle;
        private ComponentTypeHandle<PhysicsTriggerTeleportData> dataHandle;

        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private BufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private BufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            entityLock = new EntityLock(Allocator.Persistent);
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<TeleportJob>();

            query = SystemAPI.QueryBuilder()
                .WithAll<ClipActive, PhysicsTriggerTeleportData>()
                .Build();

            entityHandle = state.GetEntityTypeHandle();
            dataHandle = state.GetComponentTypeHandle<PhysicsTriggerTeleportData>(true);

            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(false);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(false);
            
            _localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetBufferLookup<StatefulCollisionEvent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            entityLock.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            entityHandle.Update(ref state);
            dataHandle.Update(ref state);
            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);

            state.Dependency = new TeleportJob
            {
                Lock = entityLock,
                EntityHandle = entityHandle,
                DataHandle = dataHandle,
                TransformLookup = _transformLookup,
                VelocityLookup = _velocityLookup,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
            }.ScheduleParallel(query, state.Dependency);
        }

        [BurstCompile]
        private struct TeleportJob : IJobChunkWorkerBeginEnd
        {
            public EntityLock Lock;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerTeleportData> DataHandle;

            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var configs = chunk.GetNativeArray(ref DataHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var self = entities[i];
                    var cfg = configs[i];

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    {
                        for (var j = 0; j < triggers.Length; j++)
                        {
                            var evt = triggers[j];
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(self) || !LocalToWorldLookup.HasComponent(evt.EntityB))
                            {
                                continue;
                            }

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            ProcessTeleport(self, evt.EntityB, in cfg, midpoint, dir);
                        }
                    }

                    if (CollisionEventsLookup.TryGetBuffer(self, out var collisions))
                    {
                        for (var j = 0; j < collisions.Length; j++)
                        {
                            var evt = collisions[j];
                            if (evt.State != cfg.EventState || !LocalToWorldLookup.HasComponent(self) || !LocalToWorldLookup.HasComponent(evt.EntityB))
                            {
                                continue;
                            }

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;

                            var hasContact = evt.TryGetDetails(out var details);
                            var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                            var normal = hasContact ? evt.Normal : math.normalizesafe(selfPos - otherPos);

                            ProcessTeleport(self, evt.EntityB, in cfg, pt, normal);
                        }
                    }
                }
            }

            private void ProcessTeleport(Entity self, Entity other, in PhysicsTriggerTeleportData cfg, float3 contactPoint, float3 contactNormal)
            {
                var selfTargets = TargetsLookup.TryGetComponent(self, out var t) ? t : default;
                var targetToMove = PhysicsTriggerResolution.ResolveTarget(cfg.EntityToMove, self, other, selfTargets);

                if (targetToMove == Entity.Null || !TransformLookup.HasComponent(targetToMove))
                {
                    return;
                }

                var selfLtw = LocalToWorldLookup[self];
                var otherLtw = LocalToWorldLookup[other];

                var transform = PhysicsTriggerResolution.CalculateTransform(
                    cfg.PositionMode, cfg.PositionOffset, cfg.IsPositionOffsetLocal,
                    cfg.RotationMode, cfg.RotationOffsetEuler,
                    selfLtw, otherLtw, contactPoint, contactNormal);

                using (Lock.Acquire(targetToMove))
                {
                    var lt = TransformLookup[targetToMove];
                    lt.Position = transform.Position;
                    lt.Rotation = transform.Rotation;
                    TransformLookup[targetToMove] = lt;

                    if (cfg.ResetVelocity && VelocityLookup.HasComponent(targetToMove))
                    {
                        VelocityLookup[targetToMove] = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
                    }
                }
            }
        }
    }
}