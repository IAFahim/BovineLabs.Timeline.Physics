using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using FloatRange = Unity.Physics.Math.FloatRange;

namespace BovineLabs.Timeline.Physics.Chains
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainGrabSystem : ISystem
    {
        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ChainLink> _linkHandle;
        private ComponentTypeHandle<ChainGrabConfig> _configHandle;
        private ComponentTypeHandle<ChainGrabArmed> _armedHandle;
        private ComponentTypeHandle<ChainLinkGrabbed> _grabbedHandle;

        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeBufferLookup<StatefulCollisionEvent> _collisionLookup;
        private UnsafeBufferLookup<StatefulTriggerEvent> _triggerLookup;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityHandle = state.GetEntityTypeHandle();
            _linkHandle = state.GetComponentTypeHandle<ChainLink>(true);
            _configHandle = state.GetComponentTypeHandle<ChainGrabConfig>(true);
            _armedHandle = state.GetComponentTypeHandle<ChainGrabArmed>(true);
            _grabbedHandle = state.GetComponentTypeHandle<ChainLinkGrabbed>(true);

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _collisionLookup = state.GetUnsafeBufferLookup<StatefulCollisionEvent>(true);
            _triggerLookup = state.GetUnsafeBufferLookup<StatefulTriggerEvent>(true);

            _query = SystemAPI.QueryBuilder()
                .WithAll<ChainLink, ChainGrabConfig, ChainGrabArmed>()
                .WithDisabled<ChainLinkGrabbed>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityHandle.Update(ref state);
            _linkHandle.Update(ref state);
            _configHandle.Update(ref state);
            _armedHandle.Update(ref state);
            _grabbedHandle.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _collisionLookup.Update(ref state);
            _triggerLookup.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new GrabJob
            {
                EntityHandle = _entityHandle,
                LinkHandle = _linkHandle,
                ConfigHandle = _configHandle,
                ArmedHandle = _armedHandle,
                GrabbedHandle = _grabbedHandle,
                LocalToWorldLookup = _localToWorldLookup,
                ColliderLookup = _colliderLookup,
                TargetsLookup = _targetsLookup,
                CollisionLookup = _collisionLookup,
                TriggerLookup = _triggerLookup,
                ECB = ecb
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct GrabJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ChainLink> LinkHandle;
            [ReadOnly] public ComponentTypeHandle<ChainGrabConfig> ConfigHandle;
            [ReadOnly] public ComponentTypeHandle<ChainGrabArmed> ArmedHandle;
            [ReadOnly] public ComponentTypeHandle<ChainLinkGrabbed> GrabbedHandle;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulCollisionEvent> CollisionLookup;
            [ReadOnly] public UnsafeBufferLookup<StatefulTriggerEvent> TriggerLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var links = chunk.GetNativeArray(ref LinkHandle);
                var configs = chunk.GetNativeArray(ref ConfigHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    if (chunk.IsComponentEnabled(ref GrabbedHandle, i)) continue;

                    var linkEntity = entities[i];
                    var link = links[i];
                    var config = configs[i];

                    if (!TryFindHit(linkEntity, config.HitMask, out var other)) continue;
                    if (!LocalToWorldLookup.TryGetComponent(linkEntity, out var linkL2W)) continue;
                    if (!LocalToWorldLookup.TryGetComponent(other, out var otherL2W)) continue;

                    var joint = config.Mode switch
                    {
                        ChainGrabMode.Reel => CreateReel(unfilteredChunkIndex, link.Root, other, in otherL2W,
                            in config),
                        ChainGrabMode.Wrap => CreateStick(unfilteredChunkIndex, linkEntity, other, in linkL2W,
                            in otherL2W, in config),
                        _ => CreateStick(unfilteredChunkIndex, linkEntity, other, in linkL2W, in otherL2W, in config)
                    };

                    if (joint == Entity.Null) continue;

                    ECB.AppendToBuffer(unfilteredChunkIndex, link.Root, new ChainAnchor
                    {
                        Joint = joint,
                        Link = linkEntity
                    });
                    ECB.SetComponentEnabled<ChainLinkGrabbed>(unfilteredChunkIndex, linkEntity, true);
                    ECB.SetComponentEnabled<ChainGrabArmed>(unfilteredChunkIndex, linkEntity, false);
                }
            }

            private bool TryFindHit(Entity self, uint mask, out Entity other)
            {
                other = Entity.Null;

                if (CollisionLookup.TryGetBuffer(self, out var collisions))
                    foreach (var evt in collisions)
                        if (evt.State == StatefulEventState.Enter && PassesMask(evt.EntityB, mask))
                        {
                            other = evt.EntityB;
                            return true;
                        }

                if (TriggerLookup.TryGetBuffer(self, out var triggers))
                    foreach (var evt in triggers)
                        if (evt.State == StatefulEventState.Enter && PassesMask(evt.EntityB, mask))
                        {
                            other = evt.EntityB;
                            return true;
                        }

                return false;
            }

            private bool PassesMask(Entity other, uint mask)
            {
                if (mask == 0) return true;
                if (!ColliderLookup.TryGetComponent(other, out var collider) || !collider.IsValid) return false;
                return (collider.Value.Value.GetCollisionFilter().BelongsTo & mask) != 0;
            }

            private Entity CreateStick(int sortKey, Entity link, Entity other, in LocalToWorld linkL2W,
                in LocalToWorld otherL2W, in ChainGrabConfig config)
            {
                var anchorB = math.transform(math.inverse(otherL2W.Value), linkL2W.Position);
                var joint = ECB.CreateEntity(sortKey);
                ECB.AddComponent(sortKey, joint, new PhysicsConstrainedBodyPair(link, other, config.EnableCollision));
                ECB.AddComponent(sortKey, joint, PhysicsJoint.CreateBallAndSocket(float3.zero, anchorB));
                return joint;
            }

            private Entity CreateReel(int sortKey, Entity root, Entity other, in LocalToWorld otherL2W,
                in ChainGrabConfig config)
            {
                var anchor = root;
                if (config.ReelAnchor != Target.Self && config.ReelAnchor != Target.None &&
                    TargetsLookup.TryGetComponent(root, out var targets))
                    anchor = targets.Get(config.ReelAnchor, root);

                if (!LocalToWorldLookup.TryGetComponent(anchor, out var anchorL2W)) return Entity.Null;

                var distance = math.distance(anchorL2W.Position, otherL2W.Position);
                var joint = ECB.CreateEntity(sortKey);
                ECB.AddComponent(sortKey, joint, new PhysicsConstrainedBodyPair(anchor, other, config.EnableCollision));
                ECB.AddComponent(sortKey, joint, PhysicsJoint.CreateLimitedDistance(float3.zero, float3.zero,
                    new FloatRange(config.ReelMinDistance, distance)));
                ECB.AddComponent(sortKey, joint, new ChainReel
                {
                    Speed = config.ReelSpeed,
                    MinDistance = config.ReelMinDistance
                });
                return joint;
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [UpdateAfter(typeof(ChainGrabSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainReelSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 1e-5f) return;

            foreach (var (joint, reel) in
                     SystemAPI.Query<RefRW<PhysicsJoint>, RefRO<ChainReel>>())
            {
                var constraints = joint.ValueRO.GetConstraints();
                if (constraints.Length == 0) continue;

                var c = constraints[0];
                c.Max = math.max(reel.ValueRO.MinDistance, c.Max - reel.ValueRO.Speed * dt);
                c.Min = math.min(c.Min, c.Max);
                constraints[0] = c;
                joint.ValueRW.SetConstraints(constraints);
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [UpdateAfter(typeof(ChainReelSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct ChainReleaseSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSystem = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (anchors, releaseEntity) in
                     SystemAPI.Query<DynamicBuffer<ChainAnchor>>()
                         .WithAll<ChainReleaseRequest>()
                         .WithEntityAccess())
            {
                for (var i = 0; i < anchors.Length; i++)
                {
                    if (anchors[i].Joint != Entity.Null)
                        ecb.DestroyEntity(anchors[i].Joint);

                    if (anchors[i].Link != Entity.Null)
                    {
                        ecb.SetComponentEnabled<ChainLinkGrabbed>(anchors[i].Link, false);
                        ecb.SetComponentEnabled<ChainGrabArmed>(anchors[i].Link, true);
                    }
                }

                anchors.Clear();
                ecb.SetComponentEnabled<ChainReleaseRequest>(releaseEntity, false);
            }
        }
    }
}