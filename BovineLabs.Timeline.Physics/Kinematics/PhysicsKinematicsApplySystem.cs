using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Kinematics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsKinematicsApplySystem : ISystem
    {
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveForce> _activeForceHandle;
        private ComponentTypeHandle<PhysicsForceState> _forceStateHandle;
        private ComponentTypeHandle<PhysicsForceRandom> _forceRandomHandle;
        private ComponentTypeHandle<PhysicsVelocity> _physicsVelocityHandle;
        private ComponentTypeHandle<PendingVelocityReset> _pendingResetHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;
        private BufferTypeHandle<PendingExternalForce> _pendingExternalForceHandle;

        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);

            _entityHandle = state.GetEntityTypeHandle();
            _activeForceHandle = state.GetComponentTypeHandle<ActiveForce>(true);
            _forceStateHandle = state.GetComponentTypeHandle<PhysicsForceState>();
            _forceRandomHandle = state.GetComponentTypeHandle<PhysicsForceRandom>();
            _physicsVelocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>(true);
            _pendingResetHandle = state.GetComponentTypeHandle<PendingVelocityReset>();
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
            _pendingExternalForceHandle = state.GetBufferTypeHandle<PendingExternalForce>();

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
            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            _entityHandle.Update(ref state);
            _activeForceHandle.Update(ref state);
            _forceStateHandle.Update(ref state);
            _forceRandomHandle.Update(ref state);
            _physicsVelocityHandle.Update(ref state);
            _pendingResetHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);
            _pendingExternalForceHandle.Update(ref state);

            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);
            _pendingVelocityHandle.Update(ref state);

            state.Dependency = new AppendForceJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeForceHandle,
                StateTypeHandle = _forceStateHandle,
                RandomHandle = _forceRandomHandle,
                VelocityHandle = _physicsVelocityHandle,
                PendingResetHandle = _pendingResetHandle,
                PendingForceHandle = _pendingForceHandle,
                PendingExternalForceHandle = _pendingExternalForceHandle,
                TargetsLookup = _targetsLookup,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup,
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
                PendingResetHandle = _pendingResetHandle,
                PendingVelocityHandle = _pendingVelocityHandle,
                TargetsLookup = _targetsLookup,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_velocityQuery, state.Dependency);
        }

        internal static void RequestVelocityReset(in ArchetypeChunk chunk,
            ref ComponentTypeHandle<PendingVelocityReset> resetHandle, NativeArray<PendingVelocityReset> resets,
            int index, VelocityResetFlags flags)
        {
            resets[index] = new PendingVelocityReset { Flags = resets[index].Flags | flags };
            chunk.SetComponentEnabled(ref resetHandle, index, true);
        }

        [BurstCompile]
        private struct AppendForceJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveForce> ActiveHandle;
            public ComponentTypeHandle<PhysicsForceState> StateTypeHandle;
            public ComponentTypeHandle<PhysicsForceRandom> RandomHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            public ComponentTypeHandle<PendingVelocityReset> PendingResetHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;
            public BufferTypeHandle<PendingExternalForce> PendingExternalForceHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
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

                // External channel is auto-baked onto every dynamic body alongside PendingForce; guard anyway so an
                // opted-out body falls back to Intent rather than dropping the force.
                var hasExternal = chunk.Has(ref PendingExternalForceHandle);
                var externalForces = hasExternal ? chunk.GetBufferAccessor(ref PendingExternalForceHandle) : default;

                var hasRandom = chunk.Has(ref RandomHandle);
                var randoms = hasRandom ? chunk.GetNativeArray(ref RandomHandle) : default;

                var hasVelocity = chunk.Has(ref VelocityHandle);
                var velocities = hasVelocity ? chunk.GetNativeArray(ref VelocityHandle) : default;

                var hasReset = chunk.Has(ref PendingResetHandle);
                var resets = hasReset ? chunk.GetNativeArray(ref PendingResetHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var body = entities[i];
                    var config = actives[i].Config;
                    var state = states[i];

                    // Impulse: one-shot latch. Continuous: integrate against the CLIP-active time the (render-rate)
                    // track accumulated (state.ElapsedTime), applying only the unapplied delta — so the total
                    // impulse = force × active-duration regardless of how many fixed steps land in the window.
                    // Using the fixed-step DeltaTime here instead made continuous non-deterministic (the fixed-step
                    // group ticks a variable number of times per rendered frame), while impulse stayed reliable.
                    float timeScale;
                    if (config.Mode == PhysicsForceMode.Impulse)
                    {
                        if (state.Fired) continue;
                        timeScale = 1f;
                    }
                    else
                    {
                        timeScale = state.ElapsedTime - state.AppliedTime;
                        if (timeScale <= 1e-6f) continue;
                    }

                    var targets = TargetsLookup.TryGetComponent(body, out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, body, targets,
                        LinkSources, Links, StatLookup);

                    if (math.abs(multiplier) < 1e-5f) continue;

                    if (!TryResolveLinearForce(chunk, i, body, in config, in targets, hasRandom, randoms,
                            hasVelocity, velocities, ref state, out var linForce))
                        continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, body, in TargetsLookup,
                        in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var angForce);

                    if (hasReset && config.ResetVelocityOnFire != VelocityResetFlags.None && !state.ResetApplied)
                    {
                        RequestVelocityReset(in chunk, ref PendingResetHandle, resets, i,
                            config.ResetVelocityOnFire);
                        state.ResetApplied = true;
                    }

                    var linear = linForce * timeScale * multiplier;
                    var angular = angForce * timeScale * multiplier;
                    if (config.Channel == MotionChannel.External && hasExternal)
                    {
                        externalForces[i].Add(new PendingExternalForce { Linear = linear, Angular = angular });
                    }
                    else
                    {
                        pendingForces[i].Add(new PendingForce { Linear = linear, Angular = angular });
                    }

                    if (config.Mode == PhysicsForceMode.Impulse)
                    {
                        state.Fired = true;
                    }
                    else
                    {
                        state.AppliedTime = state.ElapsedTime;
                    }

                    states[i] = state;
                }
            }

            private bool TryResolveLinearForce(in ArchetypeChunk chunk, int i, Entity body,
                in PhysicsForceData config, in Targets targets, bool hasRandom,
                NativeArray<PhysicsForceRandom> randoms, bool hasVelocity, NativeArray<PhysicsVelocity> velocities,
                ref PhysicsForceState state, out float3 linForce)
            {
                switch (config.DirectionMode)
                {
                    case PhysicsForceDirectionMode.FixedVector:
                        PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, body, in TargetsLookup,
                            in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out linForce);
                        return true;

                    case PhysicsForceDirectionMode.TowardTarget:
                    case PhysicsForceDirectionMode.AwayFromTarget:
                        return TryResolveTargetForce(body, in config, in targets, out linForce);

                    default:
                        if (!TryResolveDynamicDirection(chunk, i, body, in config, hasRandom, randoms, hasVelocity,
                                velocities, ref state, out var direction))
                        {
                            linForce = float3.zero;
                            return false;
                        }

                        linForce = direction * config.Magnitude;
                        return true;
                }
            }

            private bool TryResolveTargetForce(Entity body, in PhysicsForceData config, in Targets targets,
                out float3 linForce)
            {
                linForce = float3.zero;
                var targetEntity = body;
                if (config.DirectionTarget != Target.None)
                {
                    var baseTarget = targets.Get(config.DirectionTarget, body);
                    if (baseTarget != Entity.Null)
                    {
                        targetEntity = baseTarget;
                        if (config.DirectionTargetLinkKey != 0 &&
                            EntityLinkResolver.TryResolve(baseTarget, config.DirectionTargetLinkKey,
                                LinkSources, Links, out var linked))
                            targetEntity = linked;
                    }
                }

                var selfPos = PhysicsMath.ResolvePosition(body, in LocalTransformLookup, in LocalToWorldLookup,
                    in ParentLookup);
                var targetPos = PhysicsMath.ResolvePosition(targetEntity, in LocalTransformLookup,
                    in LocalToWorldLookup, in ParentLookup);
                var diff = targetPos - selfPos;
                var distSq = math.lengthsq(diff);
                if (distSq <= 1e-5f) return false;

                linForce = diff * math.rsqrt(distSq) * config.Magnitude;
                if (config.DirectionMode == PhysicsForceDirectionMode.AwayFromTarget) linForce = -linForce;
                return true;
            }

            private bool TryResolveDynamicDirection(in ArchetypeChunk chunk, int i, Entity body,
                in PhysicsForceData config, bool hasRandom, NativeArray<PhysicsForceRandom> randoms,
                bool hasVelocity, NativeArray<PhysicsVelocity> velocities, ref PhysicsForceState state,
                out float3 direction)
            {
                if (config.LatchDirection && state.DirectionLatched)
                {
                    direction = state.LatchedDirection;
                    return true;
                }

                switch (config.DirectionMode)
                {
                    case PhysicsForceDirectionMode.RandomSphere:
                    {
                        var rng = NextRandom(hasRandom, randoms, i, body, config.Seed);
                        direction = rng.NextFloat3Direction();
                        if (hasRandom) randoms[i] = new PhysicsForceRandom { Value = rng };
                        break;
                    }

                    case PhysicsForceDirectionMode.RandomCone:
                    {
                        var rng = NextRandom(hasRandom, randoms, i, body, config.Seed);
                        var az = config.ConeAzimuthCenter +
                                 rng.NextFloat(-config.ConeAzimuthHalfRange, config.ConeAzimuthHalfRange);
                        var el = config.ConeElevationCenter +
                                 rng.NextFloat(-config.ConeElevationHalfRange, config.ConeElevationHalfRange);
                        el = math.clamp(el, -math.PI * 0.5f + 0.01f, math.PI * 0.5f - 0.01f);
                        if (hasRandom) randoms[i] = new PhysicsForceRandom { Value = rng };

                        var cosEl = math.cos(el);
                        var localDir = new float3(cosEl * math.sin(az), math.sin(el), cosEl * math.cos(az));
                        PhysicsMath.ResolveSpaceVector(config.Space, localDir, body, in TargetsLookup,
                            in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out direction);
                        break;
                    }

                    case PhysicsForceDirectionMode.AlongVelocity:
                    case PhysicsForceDirectionMode.AgainstVelocity:
                    {
                        direction = float3.zero;
                        if (!hasVelocity) return false;

                        var linear = velocities[i].Linear;
                        var speedSq = math.lengthsq(linear);
                        if (speedSq <= 1e-8f) return false;

                        direction = linear * math.rsqrt(speedSq);
                        if (config.DirectionMode == PhysicsForceDirectionMode.AgainstVelocity)
                            direction = -direction;
                        break;
                    }

                    default:
                        direction = float3.zero;
                        return false;
                }

                if (config.LatchDirection)
                {
                    state.DirectionLatched = true;
                    state.LatchedDirection = direction;
                }

                return true;
            }

            private static Random NextRandom(bool hasRandom, NativeArray<PhysicsForceRandom> randoms, int i,
                Entity body, uint seed)
            {
                var rng = hasRandom ? randoms[i].Value : default;
                if (rng.state == 0)
                {
                    rng = Random.CreateFromIndex(math.hash(new uint3(seed, (uint)body.Index, (uint)body.Version)));
                    if (rng.state == 0) rng.state = 0x6E624EB7;
                }

                return rng;
            }
        }

        [BurstCompile]
        private struct AppendVelocityJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveVelocity> ActiveHandle;
            public ComponentTypeHandle<PhysicsVelocityState> StateTypeHandle;
            public ComponentTypeHandle<PendingVelocityReset> PendingResetHandle;
            public BufferTypeHandle<PendingVelocity> PendingVelocityHandle;

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var pendingVelocities = chunk.GetBufferAccessor(ref PendingVelocityHandle);

                var hasReset = chunk.Has(ref PendingResetHandle);
                var resets = hasReset ? chunk.GetNativeArray(ref PendingResetHandle) : default;

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

                    var targets = TargetsLookup.TryGetComponent(body, out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, body, targets,
                        LinkSources, Links, StatLookup);

                    if (math.abs(multiplier) < 1e-5f) continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, body, in TargetsLookup,
                        in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var linVel);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, body, in TargetsLookup,
                        in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var angVel);

                    if (hasReset && config.ResetVelocityOnFire != VelocityResetFlags.None && !state.ResetApplied)
                    {
                        RequestVelocityReset(in chunk, ref PendingResetHandle, resets, i,
                            config.ResetVelocityOnFire);
                        state.ResetApplied = true;
                    }

                    var timeScale = isInstant ? 1f : DeltaTime;
                    pendingVelocities[i].Add(new PendingVelocity
                    {
                        Linear = linVel * timeScale * multiplier,
                        Angular = angVel * timeScale * multiplier
                    });

                    if (isInstant) state.Fired = true;

                    states[i] = state;
                }
            }
        }
    }
}