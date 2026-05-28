using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Core.Extensions;
using BovineLabs.Essence.Data;
using BovineLabs.Timeline.EntityLinks.Data;
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
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsRicochetApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveRicochet> _activeHandle;
        private ComponentTypeHandle<PhysicsRicochetState> _stateHandle;
        
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private UnsafeComponentLookup<LocalToWorld> _ltwLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private BufferLookup<Stat> _statLookup;
        private ConditionEventWriter.Lookup _writers;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveRicochet>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsRicochetState>();

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _ltwLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
            _writers.Create(ref state);

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsRicochetState>()
                .WithAll<ActiveRicochet>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
                
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);

            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _statLookup.Update(ref state);
            _writers.Update(ref state);

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            state.Dependency = new ApplyJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                LtwLookup = _ltwLookup,
                ColliderLookup = _colliderLookup,
                StatLookup = _statLookup,
                Writers = _writers,
                CollisionWorld = physicsWorld
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<ActiveRicochet> ActiveHandle;
            public ComponentTypeHandle<PhysicsRicochetState> StateHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            public ConditionEventWriter.Lookup Writers;

            [ReadOnly] public CollisionWorld CollisionWorld;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var states = chunk.GetNativeArray(ref StateHandle);
                
                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];
                    var entity = entities[i];

                    if (isActive && !state.Fired)
                    {
                        var config = actives[i].Config;
                        var targets = TargetsLookup.HasComponent(entity) ? TargetsLookup[entity] : default;
                        
                        float3 origin = float3.zero;
                        float3 direction = math.forward();
                        
                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RayOrigin, config.RayOriginLinkKey, entity, Entity.Null, targets, LinkSources, Links, out var originEntity) && LtwLookup.HasComponent(originEntity))
                        {
                            origin = LtwLookup[originEntity].Position;
                        }
                        
                        if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RayDirection, config.RayDirectionLinkKey, entity, Entity.Null, targets, LinkSources, Links, out var dirEntity) && LtwLookup.HasComponent(dirEntity))
                        {
                            direction = math.rotate(LtwLookup[dirEntity].Rotation, math.forward());
                        }

                        var multiplier = StatStrengthUtility.Resolve(in config.Strength, entity, targets, LinkSources, Links, StatLookup);
                        var remainingDistance = config.MaxDistance * multiplier;
                        var bounceCount = 0;
                        
                        var currentPos = origin;
                        var currentDir = math.normalize(direction);

                        while (bounceCount <= config.MaxBounces && remainingDistance > 0)
                        {
                            var rayInput = new RaycastInput
                            {
                                Start = currentPos,
                                End = currentPos + currentDir * remainingDistance,
                                Filter = new CollisionFilter
                                {
                                    BelongsTo = ~0u,
                                    CollidesWith = config.RicochetMask | config.TerminalHitMask,
                                    GroupIndex = 0
                                }
                            };

                            if (!CollisionWorld.CastRay(rayInput, out var hit))
                                break;
                            
                            var dist = math.distance(currentPos, hit.Position);
                            remainingDistance -= dist;
                            var hitEntity = hit.Entity;
                            
                            var surfaceBelongsTo = 0u;
                            if (ColliderLookup.HasComponent(hitEntity))
                            {
                                var col = ColliderLookup[hitEntity];
                                if (col.IsValid)
                                {
                                    surfaceBelongsTo = col.Value.Value.GetCollisionFilter().BelongsTo;
                                }
                            }

                            var grazingAngle = math.acos(math.abs(math.dot(currentDir, hit.SurfaceNormal)));

                            if (grazingAngle >= config.MinGrazingAngle || (surfaceBelongsTo & config.TerminalHitMask) != 0)
                            {
                                var hitTargets = TargetsLookup.HasComponent(hitEntity) ? TargetsLookup[hitEntity] : default;
                                if (config.HitConditionKey != 0 && PhysicsTriggerResolution.TryResolveLinkedTarget(config.HitRouteTo, config.HitRouteLinkKey, entity, hitEntity, hitTargets, LinkSources, Links, out var target))
                                {
                                    if (Writers.TryGet(target, out var writer))
                                        writer.Trigger(config.HitConditionKey, bounceCount);
                                }
                                break;
                            }
                            
                            if ((surfaceBelongsTo & config.RicochetMask) != 0)
                            {
                                currentDir = currentDir - 2f * math.dot(currentDir, hit.SurfaceNormal) * hit.SurfaceNormal;
                                currentPos = hit.Position + currentDir * 0.01f;
                                bounceCount++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        state.Fired = true;
                        states[i] = state;
                    }
                    else if (!isActive && state.Fired)
                    {
                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }
        }
    }
}
