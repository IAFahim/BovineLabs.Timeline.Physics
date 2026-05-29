using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsPidApplySystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveLinearPid> _activeLinearHandle;
        private ComponentTypeHandle<PhysicsLinearPIDState> _linearStateHandle;
        private ComponentTypeHandle<ActiveAngularPid> _activeAngularHandle;
        private ComponentTypeHandle<PhysicsAngularPIDState> _angularStateHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;

        private EntityQuery _linearQuery;
        private EntityQuery _angularQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);

            _entityHandle = state.GetEntityTypeHandle();
            _activeLinearHandle = state.GetComponentTypeHandle<ActiveLinearPid>(true);
            _linearStateHandle = state.GetComponentTypeHandle<PhysicsLinearPIDState>();
            _activeAngularHandle = state.GetComponentTypeHandle<ActiveAngularPid>(true);
            _angularStateHandle = state.GetComponentTypeHandle<PhysicsAngularPIDState>();
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();

            _linearQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsLinearPIDState, PendingForce>()
                .WithAll<ActiveLinearPid, LocalToWorld>()
                .Build();

            _angularQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsAngularPIDState, PendingForce>()
                .WithAll<ActiveAngularPid, LocalToWorld>()
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
            _activeLinearHandle.Update(ref state);
            _linearStateHandle.Update(ref state);
            _activeAngularHandle.Update(ref state);
            _angularStateHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);

            state.Dependency = new AppendLinearJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeLinearHandle,
                StateTypeHandle = _linearStateHandle,
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
                ActiveHandle = _activeAngularHandle,
                StateTypeHandle = _angularStateHandle,
                PendingForceHandle = _pendingForceHandle,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_angularQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendLinearJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveLinearPid> ActiveHandle;
            public ComponentTypeHandle<PhysicsLinearPIDState> StateTypeHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
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
                    var transform = TransformLookup[body];

                    PhysicsMath.ResolveLinearPidTarget(transform, config, body,
                        in TargetsLookup, in TransformLookup,
                        out var resolvedTarget);

                    float3 targetPos;
                    if (config.TargetMode == PidLinearTargetMode.InitialLocal)
                    {
                        if (!state.State.IsInitialized)
                            state.State.CapturedTargetPosition = resolvedTarget;
                        targetPos = state.State.CapturedTargetPosition;
                    }
                    else
                    {
                        targetPos = resolvedTarget;
                    }

                    var capturedPos = state.State.CapturedTargetPosition;
                    var error = targetPos - transform.Position;

                    PhysicsMath.ComputePidForce(error, config.Tuning, state.State, DeltaTime,
                        out var force, out var nextState);

                    nextState.CapturedTargetPosition = capturedPos;

                    if (config.StopThreshold > 0f && math.length(error) < config.StopThreshold)
                    {
                        state.State = nextState;
                        states[i] = state;
                        continue;
                    }

                    var targets = TargetsLookup.TryGetComponent(body, out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, body, targets,
                        LinkSources, Links, StatLookup);

                    force *= config.Strength * multiplier;

                    if (math.lengthsq(force) > 1e-5f)
                        pendingForces[i].Add(new PendingForce
                        {
                            Linear = force * DeltaTime,
                            Angular = float3.zero
                        });

                    state.State = nextState;
                    states[i] = state;
                }
            }
        }

        [BurstCompile]
        private struct AppendAngularJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveAngularPid> ActiveHandle;
            public ComponentTypeHandle<PhysicsAngularPIDState> StateTypeHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
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
                    var transform = TransformLookup[body];

                    PhysicsMath.ResolveAngularPidTarget(transform, config, body,
                        in TargetsLookup, in TransformLookup, out var targetRot);

                    PhysicsMath.ComputeAngularError(new quaternion(transform.Value), targetRot, out var error);

                    PhysicsMath.ComputePidForce(error, config.Tuning, state.State, DeltaTime,
                        out var torque, out var nextState);

                    if (config.StopThreshold > 0f && math.degrees(math.length(error)) < config.StopThreshold)
                    {
                        state.State = nextState;
                        states[i] = state;
                        continue;
                    }

                    var targets = TargetsLookup.TryGetComponent(body, out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, body, targets,
                        LinkSources, Links, StatLookup);

                    torque *= config.Strength * multiplier;

                    if (math.lengthsq(torque) > 1e-5f)
                        pendingForces[i].Add(new PendingForce
                        {
                            Linear = float3.zero,
                            Angular = torque * DeltaTime
                        });

                    state.State = nextState;
                    states[i] = state;
                }
            }
        }
    }
}