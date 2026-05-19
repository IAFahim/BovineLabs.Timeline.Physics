using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
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
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsKinematicsApplySystem : ISystem
    {
        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveForce> _activeForceHandle;
        private ComponentTypeHandle<PhysicsForceState> _forceStateHandle;
        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private ComponentTypeHandle<PhysicsVelocity> _physicsVelocityHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AppendForceJob>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AppendVelocityJob>();

            _forceQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PendingForce, PhysicsForceState>()
                .WithAll<ActiveForce, LocalTransform>()
                .Build();

            _velocityQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PendingVelocity, PhysicsVelocityState>()
                .WithAll<ActiveVelocity, LocalTransform, PhysicsVelocity>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _activeForceHandle = state.GetComponentTypeHandle<ActiveForce>(true);
            _forceStateHandle = state.GetComponentTypeHandle<PhysicsForceState>();
            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);
            _velocityStateHandle = state.GetComponentTypeHandle<PhysicsVelocityState>();
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _physicsVelocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>(true);
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
            _pendingVelocityHandle = state.GetBufferTypeHandle<PendingVelocity>();

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            _entityHandle.Update(ref state);
            _activeForceHandle.Update(ref state);
            _forceStateHandle.Update(ref state);
            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _physicsVelocityHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);
            _pendingVelocityHandle.Update(ref state);
            _targetsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            state.Dependency = new AppendForceJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveForceHandle = _activeForceHandle,
                ForceStateHandle = _forceStateHandle,
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
                ActiveVelocityHandle = _activeVelocityHandle,
                VelocityStateHandle = _velocityStateHandle,
                TransformHandle = _transformHandle,
                PhysicsVelocityHandle = _physicsVelocityHandle,
                PendingVelocityHandle = _pendingVelocityHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_velocityQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendForceJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveForce> ActiveForceHandle;
            public ComponentTypeHandle<PhysicsForceState> ForceStateHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var forces = chunk.GetNativeArray(ref ActiveForceHandle);
                var states = chunk.GetNativeArray(ref ForceStateHandle);
                var bufferAccessor = chunk.GetBufferAccessor(ref PendingForceHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var config = forces[i].Config;
                    var s = states[i];

                    if (config.Mode == PhysicsForceMode.Impulse && s.Fired)
                        continue;

                    var targets = TargetsLookup.HasComponent(entities[i]) ? TargetsLookup[entities[i]] : default;
                    float multiplier = StatStrengthUtility.Resolve(in config.Strength, entities[i], targets, LinkSources, Links, StatLookup);

                    if (math.abs(multiplier) < 1e-5f)
                        continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                        in TransformLookup, out var linForce);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                        in TransformLookup, out var angForce);

                    var timeScale = config.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;

                    bufferAccessor[i].Add(new PendingForce
                    {
                        Linear = linForce * timeScale * multiplier,
                        Angular = angForce * timeScale * multiplier
                    });

                    if (config.Mode == PhysicsForceMode.Impulse)
                    {
                        s.Fired = true;
                        states[i] = s;
                    }
                }
            }
        }

        [BurstCompile]
        private struct AppendVelocityJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveVelocity> ActiveVelocityHandle;
            public ComponentTypeHandle<PhysicsVelocityState> VelocityStateHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> PhysicsVelocityHandle;
            public BufferTypeHandle<PendingVelocity> PendingVelocityHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var velocities = chunk.GetNativeArray(ref ActiveVelocityHandle);
                var states = chunk.GetNativeArray(ref VelocityStateHandle);
                var currentVelocities = chunk.GetNativeArray(ref PhysicsVelocityHandle);
                var bufferAccessor = chunk.GetBufferAccessor(ref PendingVelocityHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var config = velocities[i].Config;
                    var s = states[i];

                    var isInstant = config.Mode == PhysicsVelocityMode.SetInstant ||
                                    config.Mode == PhysicsVelocityMode.AddInstant;
                    if (isInstant && s.Fired)
                        continue;

                    var targets = TargetsLookup.HasComponent(entities[i]) ? TargetsLookup[entities[i]] : default;
                    float multiplier = StatStrengthUtility.Resolve(in config.Strength, entities[i], targets, LinkSources, Links, StatLookup);

                    if (math.abs(multiplier) < 1e-5f)
                        continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                        in TransformLookup, out var linVel);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                        in TransformLookup, out var angVel);

                    var isSet = config.Mode == PhysicsVelocityMode.SetContinuous ||
                                config.Mode == PhysicsVelocityMode.SetInstant;

                    float3 linearDelta;
                    float3 angularDelta;

                    if (isSet)
                    {
                        var current = currentVelocities[i];
                        linearDelta = (linVel * multiplier) - current.Linear;
                        angularDelta = (angVel * multiplier) - current.Angular;
                    }
                    else
                    {
                        var timeScale = isInstant ? 1f : DeltaTime;
                        linearDelta = linVel * timeScale * multiplier;
                        angularDelta = angVel * timeScale * multiplier;
                    }

                    bufferAccessor[i].Add(new PendingVelocity
                    {
                        Linear = linearDelta,
                        Angular = angularDelta
                    });

                    if (isInstant)
                    {
                        s.Fired = true;
                        states[i] = s;
                    }
                }
            }
        }
    }
}