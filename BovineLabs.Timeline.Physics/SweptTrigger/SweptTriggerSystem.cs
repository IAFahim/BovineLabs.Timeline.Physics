using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.TriggerEvents;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.SweptTrigger
{
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsTriggerQuerySystem))]
    [UpdateBefore(typeof(PhysicsTriggerInstantiateSystem))]
    [UpdateBefore(typeof(PhysicsTriggerForceSystem))]
    [UpdateBefore(typeof(PhysicsTriggerConditionSystem))]
    [UpdateBefore(typeof(PhysicsBreakForceSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct SweptTriggerSystem : ISystem
    {
        private ComponentLookup<SweptTriggerConfig> _sweptConfigLookup;
        private EntityQuery _activeClipQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SweptTriggerConfig>();
            _sweptConfigLookup = state.GetComponentLookup<SweptTriggerConfig>(true);

            _activeClipQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PhysicsClipGate, TrackBinding>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld)) return;

            var capacity = math.max(1, _activeClipQuery.CalculateEntityCount());
            var activeSources = new NativeParallelHashSet<Entity>(capacity, state.WorldUpdateAllocator);

            _sweptConfigLookup.Update(ref state);

            var collectHandle = new CollectActiveSourcesJob
            {
                ActiveSources = activeSources.AsParallelWriter(),
                SweptConfig = _sweptConfigLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new SweepJob
            {
                CollisionWorld = physicsWorld.PhysicsWorld.CollisionWorld,
                ActiveSources = activeSources,
                StorageInfo = SystemAPI.GetEntityStorageInfoLookup()
            }.ScheduleParallel(collectHandle);
        }

        [BurstCompile]
        [WithAll(typeof(PhysicsClipGate))]
        private partial struct CollectActiveSourcesJob : IJobEntity
        {
            public NativeParallelHashSet<Entity>.ParallelWriter ActiveSources;

            [ReadOnly] public ComponentLookup<SweptTriggerConfig> SweptConfig;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value != Entity.Null && SweptConfig.HasComponent(binding.Value))
                    ActiveSources.Add(binding.Value);
            }
        }

        [BurstCompile]
        private partial struct SweepJob : IJobEntity
        {
            [ReadOnly] public CollisionWorld CollisionWorld;

            [ReadOnly] public NativeParallelHashSet<Entity> ActiveSources;

            [ReadOnly] public EntityStorageInfoLookup StorageInfo;

            private void Execute(
                Entity entity,
                ref DynamicBuffer<StatefulTriggerEvent> events,
                ref DynamicBuffer<SweptTriggerHit> prevHits,
                ref SweptTriggerState state,
                in SweptTriggerConfig config,
                in LocalToWorld ltw)
            {
                var active = ActiveSources.Contains(entity);
                var curPos = ltw.Position;
                var curRot = ltw.Rotation;

                var current = new NativeList<Entity>(8, Allocator.Temp);

                if (active && config.Collider.IsCreated)
                {
                    var dist = new NativeList<DistanceHit>(16, Allocator.Temp);
                    var dInput = new ColliderDistanceInput(config.Collider, 0f, new RigidTransform(curRot, curPos));
                    if (CollisionWorld.CalculateDistance(dInput, ref dist))
                        for (var h = 0; h < dist.Length; h++)
                            Accumulate(ref current, dist[h].Entity, entity);

                    dist.Dispose();

                    var startPos = state.Initialized == 1 ? state.PrevPosition : curPos;
                    var startRot = state.Initialized == 1 ? state.PrevRotation : curRot;

                    var cosHalf = math.min(1f, math.abs(math.dot(startRot.value, curRot.value)));
                    var sweepAngle = 2f * math.acos(cosHalf);
                    var autoSteps = (int)math.ceil(config.TipRadius * sweepAngle / math.max(config.Thickness, 1e-4f));
                    var steps = math.clamp(math.max(config.SubSteps, autoSteps), 1, 32);

                    var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);
                    for (var s = 0; s < steps; s++)
                    {
                        var t0 = (float)s / steps;
                        var t1 = (float)(s + 1) / steps;
                        var p0 = math.lerp(startPos, curPos, t0);
                        var p1 = math.lerp(startPos, curPos, t1);
                        var rot = math.slerp(startRot, curRot, (t0 + t1) * 0.5f);

                        castHits.Clear();
                        var input = new ColliderCastInput(config.Collider, p0, p1, rot);
                        if (CollisionWorld.CastCollider(input, ref castHits))
                            for (var h = 0; h < castHits.Length; h++)
                                Accumulate(ref current, castHits[h].Entity, entity);
                    }

                    castHits.Dispose();

                    // Hysteresis retain. Acquire is tight (penetration at distance 0, or the swept cast), but a
                    // target already in contact last frame is KEPT while it stays within one blade-thickness of the
                    // collider — even on a frame where it stopped penetrating and the near-zero-length cast of a
                    // dwelling/slow weapon could not re-report it. Without this the target drops out of `current`
                    // for a frame, so the producer emits a spurious Exit then Enter next frame; with per-tick-only
                    // dedup downstream, that re-fires once-per-hit consumers (instantiate/force/condition) every
                    // few frames — the "swept trigger instantiate is always active / fires for all attacks" report.
                    if (prevHits.Length > 0)
                    {
                        var keep = new NativeList<DistanceHit>(16, Allocator.Temp);
                        var keepInput = new ColliderDistanceInput(
                            config.Collider, config.Thickness, new RigidTransform(curRot, curPos));
                        if (CollisionWorld.CalculateDistance(keepInput, ref keep))
                            for (var h = 0; h < keep.Length; h++)
                                if (ContainsBuffer(in prevHits, keep[h].Entity))
                                    Accumulate(ref current, keep[h].Entity, entity);

                        keep.Dispose();
                    }
                }

                events.Clear();

                for (var i = 0; i < current.Length; i++)
                {
                    var e = current[i];
                    var stay = ContainsBuffer(in prevHits, e);
                    events.Add(new StatefulTriggerEvent
                    {
                        EntityB = e,
                        State = stay ? StatefulEventState.Stay : StatefulEventState.Enter
                    });
                }

                for (var i = 0; i < prevHits.Length; i++)
                {
                    var e = prevHits[i].Value;

                    if (!Contains(in current, e) && StorageInfo.Exists(e))
                        events.Add(new StatefulTriggerEvent
                        {
                            EntityB = e,
                            State = StatefulEventState.Exit
                        });
                }

                prevHits.Clear();
                for (var i = 0; i < current.Length; i++) prevHits.Add(new SweptTriggerHit { Value = current[i] });

                state.PrevPosition = curPos;
                state.PrevRotation = curRot;
                state.WasActive = (byte)(active ? 1 : 0);
                state.Initialized = 1;

                current.Dispose();
            }

            private static void Accumulate(ref NativeList<Entity> list, Entity e, Entity self)
            {
                if (e == Entity.Null || e == self) return;

                if (!Contains(in list, e)) list.Add(e);
            }

            private static bool Contains(in NativeList<Entity> list, Entity e)
            {
                for (var i = 0; i < list.Length; i++)
                    if (list[i] == e)
                        return true;

                return false;
            }

            private static bool ContainsBuffer(in DynamicBuffer<SweptTriggerHit> buffer, Entity e)
            {
                for (var i = 0; i < buffer.Length; i++)
                    if (buffer[i].Value == e)
                        return true;

                return false;
            }
        }
    }
}