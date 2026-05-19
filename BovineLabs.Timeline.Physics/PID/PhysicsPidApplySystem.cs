using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsPidApplySystem : ISystem
    {
        private EntityQuery _linearQuery;
        private EntityQuery _angularQuery;

        private EntityTypeHandle _entityHandle;

        private ComponentTypeHandle<PhysicsLinearPIDState> _linearStateHandle;
        private ComponentTypeHandle<ActiveLinearPid> _activeLinearHandle;
        private ComponentTypeHandle<LocalTransform> _transformHandle;

        private ComponentTypeHandle<PhysicsAngularPIDState> _angularStateHandle;
        private ComponentTypeHandle<ActiveAngularPid> _activeAngularHandle;

        private BufferTypeHandle<PendingForce> _pendingForceHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AppendLinearJob>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AppendAngularJob>();

            _linearQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsLinearPIDState, PendingForce>()
                .WithAll<ActiveLinearPid, LocalTransform>()
                .Build();

            _angularQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsAngularPIDState, PendingForce>()
                .WithAll<ActiveAngularPid, LocalTransform>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _linearStateHandle = state.GetComponentTypeHandle<PhysicsLinearPIDState>();
            _activeLinearHandle = state.GetComponentTypeHandle<ActiveLinearPid>(true);
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _angularStateHandle = state.GetComponentTypeHandle<PhysicsAngularPIDState>();
            _activeAngularHandle = state.GetComponentTypeHandle<ActiveAngularPid>(true);
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
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
            _linearStateHandle.Update(ref state);
            _activeLinearHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _angularStateHandle.Update(ref state);
            _activeAngularHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);
            _targetsLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            state.Dependency = new AppendLinearJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                StateHandle = _linearStateHandle,
                ActiveHandle = _activeLinearHandle,
                TransformHandle = _transformHandle,
                PendingForceHandle = _pendingForceHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_linearQuery, state.Dependency);

            state.Dependency = new AppendAngularJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                StateHandle = _angularStateHandle,
                ActiveHandle = _activeAngularHandle,
                TransformHandle = _transformHandle,
                PendingForceHandle = _pendingForceHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_angularQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendLinearJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            public ComponentTypeHandle<PhysicsLinearPIDState> StateHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveLinearPid> ActiveHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
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
                var states = chunk.GetNativeArray(ref StateHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var bufferAccessor = chunk.GetBufferAccessor(ref PendingForceHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var s = states[i];
                    var config = actives[i].Config;
                    var transform = transforms[i];

                    PhysicsMath.ResolveLinearPidTarget(transform, config, entities[i],
                        in TargetsLookup, in TransformLookup,
                        out var resolvedTarget);

                    float3 targetPos;
                    if (config.TargetMode == PidLinearTargetMode.InitialLocal)
                    {
                        if (!s.State.IsInitialized)
                            s.State.CapturedTargetPosition = resolvedTarget;
                        targetPos = s.State.CapturedTargetPosition;
                    }
                    else
                    {
                        targetPos = resolvedTarget;
                    }

                    var capturedPos = s.State.CapturedTargetPosition;
                    var error = targetPos - transform.Position;

                    PhysicsMath.ComputePidForce(error, config.Tuning, s.State, DeltaTime,
                        out var force, out var nextState);

                    var targets = TargetsLookup.HasComponent(entities[i]) ? TargetsLookup[entities[i]] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, entities[i], targets,
                        LinkSources, Links, StatLookup);

                    force *= config.Strength * multiplier;

                    if (math.lengthsq(force) > 1e-5f)
                        bufferAccessor[i].Add(new PendingForce
                        {
                            Linear = force * DeltaTime,
                            Angular = float3.zero
                        });

                    nextState.CapturedTargetPosition = capturedPos;
                    s.State = nextState;
                    states[i] = s;
                }
            }
        }

        [BurstCompile]
        private struct AppendAngularJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            public ComponentTypeHandle<PhysicsAngularPIDState> StateHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveAngularPid> ActiveHandle;
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
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
                var states = chunk.GetNativeArray(ref StateHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);
                var bufferAccessor = chunk.GetBufferAccessor(ref PendingForceHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var config = actives[i].Config;
                    var transform = transforms[i];

                    PhysicsMath.ResolveAngularPidTarget(transform, config, entities[i],
                        in TargetsLookup, in TransformLookup, out var targetRot);

                    PhysicsMath.ComputeAngularError(transform.Rotation, targetRot, out var error);

                    PhysicsMath.ComputePidForce(error, config.Tuning, states[i].State, DeltaTime,
                        out var torque, out var nextState);

                    var targets = TargetsLookup.HasComponent(entities[i]) ? TargetsLookup[entities[i]] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, entities[i], targets,
                        LinkSources, Links, StatLookup);

                    torque *= config.Strength * multiplier;

                    if (math.lengthsq(torque) > 1e-5f)
                        bufferAccessor[i].Add(new PendingForce
                        {
                            Linear = float3.zero,
                            Angular = torque * DeltaTime
                        });

                    var s = states[i];
                    s.State = nextState;
                    states[i] = s;
                }
            }
        }
    }
}