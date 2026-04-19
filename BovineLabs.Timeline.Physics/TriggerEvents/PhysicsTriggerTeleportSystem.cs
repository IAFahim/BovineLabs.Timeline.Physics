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
        private EntityQuery _query;
        private EntityLock _entityLock;


        private ComponentTypeHandle<TrackBinding> _trackBindingHandle;
        private ComponentTypeHandle<PhysicsTriggerTeleportData> _dataHandle;

        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerEventsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionEventsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityLock = new EntityLock(Allocator.Persistent);
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<TeleportJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAll<ClipActive, PhysicsTriggerTeleportData>()
                .Build();

            _trackBindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);
            _dataHandle = state.GetComponentTypeHandle<PhysicsTriggerTeleportData>(true);

            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>();
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>();

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _triggerEventsLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);
            _collisionEventsLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
        }

        public void OnDestroy(ref SystemState state)
        {
            _entityLock.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _trackBindingHandle.Update(ref state);
            _dataHandle.Update(ref state);
            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _triggerEventsLookup.Update(ref state);
            _collisionEventsLookup.Update(ref state);

            state.Dependency = new TeleportJob
            {
                Lock = _entityLock,
                TrackBindingHandle = _trackBindingHandle,
                DataHandle = _dataHandle,
                TransformLookup = _transformLookup,
                VelocityLookup = _velocityLookup,
                LocalToWorldLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                TriggerEventsLookup = _triggerEventsLookup,
                CollisionEventsLookup = _collisionEventsLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct TeleportJob : IJobChunkWorkerBeginEnd
        {
            public EntityLock Lock;

            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsTriggerTeleportData> DataHandle;

            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var configs = chunk.GetNativeArray(ref DataHandle);
                var trackBindings = chunk.GetNativeArray(ref TrackBindingHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var self = trackBindings[i].Value;
                    var cfg = configs[i];

                    if (TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                        foreach (var evt in triggers)
                        {
                            if (evt.State != cfg.EventState) continue;

                            var selfPos = LocalToWorldLookup[self].Position;
                            var otherPos = LocalToWorldLookup[evt.EntityB].Position;
                            var midpoint = (selfPos + otherPos) * 0.5f;
                            var dir = math.normalizesafe(selfPos - otherPos);

                            ProcessTeleport(self, evt.EntityB, in cfg, midpoint, dir);
                        }

                    if (!CollisionEventsLookup.TryGetBuffer(self, out var collisions)) continue;
                    foreach (var evt in collisions)
                    {
                        if (evt.State != cfg.EventState) continue;

                        var selfPos = LocalToWorldLookup[self].Position;
                        var otherPos = LocalToWorldLookup[evt.EntityB].Position;

                        var hasContact = evt.TryGetDetails(out var details);
                        var pt = hasContact ? details.AverageContactPointPosition : (selfPos + otherPos) * 0.5f;
                        var normal = hasContact ? evt.Normal : math.normalizesafe(selfPos - otherPos);

                        ProcessTeleport(self, evt.EntityB, in cfg, pt, normal);
                    }
                }
            }

            private void ProcessTeleport(Entity self, Entity other, in PhysicsTriggerTeleportData cfg,
                float3 contactPoint, float3 contactNormal)
            {
                var targets = TargetsLookup[self];
                var targetToMove = PhysicsTriggerResolution.ResolveTarget(cfg.EntityToMove, self, other, targets);

                if (targetToMove == Entity.Null || !TransformLookup.HasComponent(targetToMove)) return;

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
                        VelocityLookup[targetToMove] = new PhysicsVelocity
                            { Linear = float3.zero, Angular = float3.zero };
                }
            }
        }
    }
}