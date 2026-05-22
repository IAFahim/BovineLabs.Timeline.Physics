using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    public partial struct PhysicsKinematicsApplySystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveForce> _activeForceHandle;
        private ComponentTypeHandle<PhysicsForceState> _forceStateHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;

        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);

            _entityHandle = state.GetEntityTypeHandle();
            _activeForceHandle = state.GetComponentTypeHandle<ActiveForce>(true);
            _forceStateHandle = state.GetComponentTypeHandle<PhysicsForceState>();
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();

            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);
            _velocityStateHandle = state.GetComponentTypeHandle<PhysicsVelocityState>();
            _pendingVelocityHandle = state.GetBufferTypeHandle<PendingVelocity>();

            _forceQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsForceState, PendingForce>()
                .WithAll<ActiveForce, LocalToWorld>()
                .Build();

            _velocityQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocityState, PendingVelocity>()
                .WithAll<ActiveVelocity, LocalToWorld>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;

            _targetsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            _entityHandle.Update(ref state);
            _activeForceHandle.Update(ref state);
            _forceStateHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);

            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);
            _pendingVelocityHandle.Update(ref state);

            state.Dependency = new AppendForceJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeForceHandle,
                StateTypeHandle = _forceStateHandle,
                PendingForceHandle = _pendingForceHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_forceQuery, state.Dependency);

            state.Dependency = new AppendVelocityJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeVelocityHandle,
                StateTypeHandle = _velocityStateHandle,
                PendingVelocityHandle = _pendingVelocityHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_velocityQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendForceJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveForce> ActiveHandle;
            public ComponentTypeHandle<PhysicsForceState> StateTypeHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var pendingForces = chunk.GetBufferAccessor(ref PendingForceHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var body = entities[i];
                    var config = actives[i].Config;
                    var state = states[i];

                    if (config.Mode == PhysicsForceMode.Impulse && state.Fired) continue;
                    if (config.Mode == PhysicsForceMode.Continuous && DeltaTime <= 0.0001f) continue;

                    var targets = TargetsLookup.HasComponent(body) ? TargetsLookup[body] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, body, targets,
                        LinkSources, Links, StatLookup);

                    var skip = math.abs(multiplier) < 1e-5f;

                    if (!skip)
                    {
                        PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, body, in TargetsLookup,
                            in TransformLookup, out var linForce);
                        PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, body, in TargetsLookup,
                            in TransformLookup, out var angForce);

                        var timeScale = config.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;
                        pendingForces[i].Add(new PendingForce
                        {
                            Linear = linForce * timeScale * multiplier,
                            Angular = angForce * timeScale * multiplier
                        });
                    }

                    if (config.Mode == PhysicsForceMode.Impulse)
                    {
                        state.Fired = true;
                        states[i] = state;
                    }
                }
            }
        }

        [BurstCompile]
        private struct AppendVelocityJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveVelocity> ActiveHandle;
            public ComponentTypeHandle<PhysicsVelocityState> StateTypeHandle;
            public BufferTypeHandle<PendingVelocity> PendingVelocityHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var pendingVelocities = chunk.GetBufferAccessor(ref PendingVelocityHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var body = entities[i];
                    var config = actives[i].Config;
                    var state = states[i];

                    var isInstant = config.Mode == PhysicsVelocityMode.AddInstant;
                    var isAdd = config.Mode == PhysicsVelocityMode.AddContinuous || isInstant;
                    if (!isAdd) continue;

                    if (isInstant && state.Fired) continue;
                    if (!isInstant && DeltaTime <= 0.0001f) continue;

                    var targets = TargetsLookup.HasComponent(body) ? TargetsLookup[body] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, body, targets,
                        LinkSources, Links, StatLookup);

                    var skip = math.abs(multiplier) < 1e-5f;

                    if (!skip)
                    {
                        PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, body, in TargetsLookup,
                            in TransformLookup, out var linVel);
                        PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, body, in TargetsLookup,
                            in TransformLookup, out var angVel);

                        var timeScale = isInstant ? 1f : DeltaTime;
                        pendingVelocities[i].Add(new PendingVelocity
                        {
                            Linear = linVel * timeScale * multiplier,
                            Angular = angVel * timeScale * multiplier
                        });
                    }

                    if (isInstant)
                    {
                        state.Fired = true;
                        states[i] = state;
                    }
                }
            }
        }
    }
}