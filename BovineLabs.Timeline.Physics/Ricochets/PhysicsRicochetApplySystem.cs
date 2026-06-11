using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Stats;
using BovineLabs.Timeline.Physics.TriggerEvents;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Ricochets
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsRicochetApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveRicochet> _activeHandle;
        private ComponentTypeHandle<PhysicsRicochetState> _stateHandle;

        private UnsafeComponentLookup<Targets> _targetsLookup;
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

            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
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

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            [NativeDisableParallelForRestriction] public ConditionEventWriter.Lookup Writers;

            [ReadOnly] public CollisionWorld CollisionWorld;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var states = chunk.GetNativeArray(ref StateHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];
                    var entity = entities[i];

                    if (isActive && !state.Fired)
                    {
                        FireRicochet(entity, actives[i].Config);
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

            private void FireRicochet(Entity entity, in PhysicsRicochetData config)
            {
                var targets = TargetsLookup.TryGetComponent(entity, out var t) ? t : default;

                var origin = ResolveOrigin(in config, entity, in targets);
                var direction = ResolveDirection(in config, entity, in targets);

                var multiplier = StatStrengthUtility.Resolve(in config.Strength, entity, targets, LinkSources,
                    Links, StatLookup);

                MarchRicochet(in config, entity, in targets, origin, direction, config.MaxDistance * multiplier);
            }

            private float3 ResolveOrigin(in PhysicsRicochetData config, Entity entity, in Targets targets)
            {
                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RayOrigin, config.RayOriginLinkKey,
                        entity, Entity.Null, targets, LinkSources, Links, out var originEntity) &&
                    LtwLookup.HasComponent(originEntity))
                {
                    return LtwLookup[originEntity].Position;
                }

                return float3.zero;
            }

            private float3 ResolveDirection(in PhysicsRicochetData config, Entity entity, in Targets targets)
            {
                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.RayDirection, config.RayDirectionLinkKey,
                        entity, Entity.Null, targets, LinkSources, Links, out var dirEntity) &&
                    LtwLookup.HasComponent(dirEntity))
                {
                    return math.rotate(LtwLookup[dirEntity].Rotation, math.forward());
                }

                return math.forward();
            }

            private void MarchRicochet(in PhysicsRicochetData config, Entity entity, in Targets targets, float3 origin,
                float3 direction, float remainingDistance)
            {
                var bounceCount = 0;
                var currentPos = origin;
                var currentDir = math.normalizesafe(direction, math.forward());

                while (bounceCount <= config.MaxBounces && remainingDistance > 0)
                {
                    var stepResult = PhysicsMath.StepRicochet(
                        currentPos, currentDir, remainingDistance,
                        config.RicochetMask, config.TerminalHitMask, config.MinGrazingAngle,
                        CollisionWorld, ColliderLookup);

                    if (!stepResult.HitFound)
                    {
                        break;
                    }

                    remainingDistance -= stepResult.DistanceTraveled;

                    if (stepResult.IsTerminal)
                    {
                        HandleTerminalHit(in config, entity, in stepResult, bounceCount);
                        break;
                    }

                    if (!stepResult.IsRicochet)
                    {
                        break;
                    }

                    currentDir = PhysicsMath.Reflect(currentDir, stepResult.SurfaceNormal);
                    currentPos = stepResult.HitPosition + currentDir * 0.01f;
                    bounceCount++;
                }
            }

            private void HandleTerminalHit(in PhysicsRicochetData config, Entity entity,
                in PhysicsMath.RicochetStepResult stepResult, int bounceCount)
            {
                if (config.HitConditionKey == 0)
                {
                    return;
                }

                var hitEntity = stepResult.HitEntity;
                var hitTargets = TargetsLookup.TryGetComponent(hitEntity, out var ht) ? ht : default;

                if (PhysicsTriggerResolution.TryResolveLinkedTarget(config.HitRouteTo, config.HitRouteLinkKey, entity,
                        hitEntity, hitTargets, LinkSources, Links, out var target) &&
                    Writers.TryGet(target, out var writer))
                {
                    writer.Trigger(config.HitConditionKey, bounceCount);
                }
            }
        }
    }
}