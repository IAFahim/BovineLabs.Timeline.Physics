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
        private UnsafeComponentLookup<LocalTransform> _transformLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);

            _forceQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsForceState>()
                .WithAll<TrackBinding, PhysicsForceAnimated, ClipActive>()
                .Build();

            _velocityQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocityState>()
                .WithAll<TrackBinding, PhysicsVelocityAnimated, ClipActive>()
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
            var forceAnimatedType = SystemAPI.GetComponentTypeHandle<PhysicsForceAnimated>(true);
            var forceStateType = SystemAPI.GetComponentTypeHandle<PhysicsForceState>();

            state.Dependency = new AppendForceJob
            {
                DeltaTime = dt,
                TrackBindingTypeHandle = bindingType,
                AnimatedTypeHandle = forceAnimatedType,
                StateTypeHandle = forceStateType,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ECB = ecb1
            }.ScheduleParallel(_forceQuery, state.Dependency);

            var velocityAnimatedType = SystemAPI.GetComponentTypeHandle<PhysicsVelocityAnimated>(true);
            var velocityStateType = SystemAPI.GetComponentTypeHandle<PhysicsVelocityState>();

            state.Dependency = new AppendVelocityJob
            {
                DeltaTime = dt,
                TrackBindingTypeHandle = bindingType,
                AnimatedTypeHandle = velocityAnimatedType,
                StateTypeHandle = velocityStateType,
                TargetsLookup = _targetsLookup,
                TransformLookup = _transformLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup,
                ECB = ecb2
            }.ScheduleParallel(_velocityQuery, state.Dependency);
        }

        [BurstCompile]
        private struct AppendForceJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsForceAnimated> AnimatedTypeHandle;
            public ComponentTypeHandle<PhysicsForceState> StateTypeHandle;

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

                    var animated = animateds[i];
                    var state = states[i];

                    var config = animated.Value;
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
                        ECB.AppendToBuffer(unfilteredChunkIndex, body, new PendingForce
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
            [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocityAnimated> AnimatedTypeHandle;
            public ComponentTypeHandle<PhysicsVelocityState> StateTypeHandle;

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

                    var animated = animateds[i];
                    var state = states[i];

                    var config = animated.Value;
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
                        ECB.AppendToBuffer(unfilteredChunkIndex, body, new PendingVelocity
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