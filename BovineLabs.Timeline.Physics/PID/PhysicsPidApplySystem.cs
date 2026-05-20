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
    public partial struct PhysicsPidApplySystem : ISystem
    {
        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityQuery _linearQuery;
        private EntityQuery _angularQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);

            _linearQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsLinearPIDState>()
                .WithAll<TrackBinding, PhysicsLinearPIDAnimated, ClipActive>()
                .Build();

            _angularQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsAngularPIDState>()
                .WithAll<TrackBinding, PhysicsAngularPIDAnimated, ClipActive>()
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

            var ecbSys = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb1 = ecbSys.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            var ecb2 = ecbSys.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            var linearAnimatedType = SystemAPI.GetComponentTypeHandle<PhysicsLinearPIDAnimated>(true);
            var linearStateType = SystemAPI.GetComponentTypeHandle<PhysicsLinearPIDState>();

            state.Dependency = new AppendLinearJob
            {
                DeltaTime = dt,
                TrackBindingTypeHandle = bindingType,
                AnimatedTypeHandle = linearAnimatedType,
                StateTypeHandle = linearStateType,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ECB = ecb1
            }.ScheduleParallel(_linearQuery, state.Dependency);

            var angularAnimatedType = SystemAPI.GetComponentTypeHandle<PhysicsAngularPIDAnimated>(true);
            var angularStateType = SystemAPI.GetComponentTypeHandle<PhysicsAngularPIDState>();

            state.Dependency = new AppendAngularJob
            {
                DeltaTime = dt,
                TrackBindingTypeHandle = bindingType,
                AnimatedTypeHandle = angularAnimatedType,
                StateTypeHandle = angularStateType,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ECB = ecb2
            }.ScheduleParallel(_angularQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendLinearJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsLinearPIDAnimated> AnimatedTypeHandle;
            public ComponentTypeHandle<PhysicsLinearPIDState> StateTypeHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var animateds = chunk.GetNativeArray(ref AnimatedTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var binding = bindings[i];
                    var body = binding.Value;
                    if (body == Entity.Null) continue;
                    if (!TransformLookup.HasComponent(body)) continue;

                    var animated = animateds[i];
                    var state = states[i];

                    var config = animated.Value;
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

                    var targets = TargetsLookup.HasComponent(body) ? TargetsLookup[body] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, body, targets,
                        LinkSources, Links, StatLookup);

                    force *= config.Strength * multiplier;

                    if (math.lengthsq(force) > 1e-5f)
                        ECB.AppendToBuffer(unfilteredChunkIndex, body, new PendingForce
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
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsAngularPIDAnimated> AnimatedTypeHandle;
            public ComponentTypeHandle<PhysicsAngularPIDState> StateTypeHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
                var animateds = chunk.GetNativeArray(ref AnimatedTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var binding = bindings[i];
                    var body = binding.Value;
                    if (body == Entity.Null) continue;
                    if (!TransformLookup.HasComponent(body)) continue;

                    var animated = animateds[i];
                    var state = states[i];

                    var config = animated.Value;
                    var transform = TransformLookup[body];

                    PhysicsMath.ResolveAngularPidTarget(transform, config, body,
                        in TargetsLookup, in TransformLookup, out var targetRot);

                    PhysicsMath.ComputeAngularError(transform.Rotation, targetRot, out var error);

                    PhysicsMath.ComputePidForce(error, config.Tuning, state.State, DeltaTime,
                        out var torque, out var nextState);

                    var targets = TargetsLookup.HasComponent(body) ? TargetsLookup[body] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, body, targets,
                        LinkSources, Links, StatLookup);

                    torque *= config.Strength * multiplier;

                    if (math.lengthsq(torque) > 1e-5f)
                        ECB.AppendToBuffer(unfilteredChunkIndex, body, new PendingForce
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