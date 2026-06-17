using BovineLabs.Core.Collections;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.Stats;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Splines
{
    /// <summary>
    ///     Drives each body carrying an enabled <see cref="ActiveSplineFollow" /> along its referenced spline by
    ///     appending a PID force toward a point that advances along the path. A real physical motor (writes
    ///     <see cref="PendingForce" />, resolved through inverse mass by the accumulator), so it composes with gravity
    ///     and collisions instead of teleporting the transform.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsKinematicsApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsSplineFollowApplySystem : ISystem
    {
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveSplineFollow> _activeHandle;
        private ComponentTypeHandle<PhysicsSplineFollowState> _stateHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;

        private EntityQuery _query;

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
            _activeHandle = state.GetComponentTypeHandle<ActiveSplineFollow>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsSplineFollowState>();
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsSplineFollowState, PendingForce>()
                .WithAll<ActiveSplineFollow, LocalToWorld>()
                .Build();

            state.RequireForUpdate<SplineRegistry>();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetsLookup.Update(ref state);
            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);
            _entityHandle.Update(ref state);
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);

            state.Dependency = new FollowJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Registry = SystemAPI.GetSingleton<SplineRegistry>().Map,
                EntityHandle = _entityHandle,
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                PendingForceHandle = _pendingForceHandle,
                TargetsLookup = _targetsLookup,
                LocalTransformLookup = _localTransformLookup,
                LocalToWorldLookup = _localToWorldLookup,
                ParentLookup = _parentLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        // Maps an (unbounded) progress accumulator to an evaluation parameter in [0,1] per the wrap mode.
        private static float WrapEval(float p, SplineWrap wrap)
        {
            switch (wrap)
            {
                case SplineWrap.Loop:
                    return p - math.floor(p);
                case SplineWrap.PingPong:
                    var m = math.abs(p);
                    m -= math.floor(m / 2f) * 2f; // m in [0,2)
                    return 1f - math.abs(1f - m); // triangle 0->1->0
                default:
                    return math.saturate(p);
            }
        }

        [BurstCompile]
        private struct FollowJob : IJobChunk
        {
            public float DeltaTime;
            [ReadOnly] public NativeHashMap<ushort, BlobAssetReference<BlobSpline>> Registry;

            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveSplineFollow> ActiveHandle;
            public ComponentTypeHandle<PhysicsSplineFollowState> StateHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;

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
                var states = chunk.GetNativeArray(ref StateHandle);
                var pendingForces = chunk.GetBufferAccessor(ref PendingForceHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var body = entities[i];
                    var config = actives[i].Config;
                    var state = states[i];

                    if (!Registry.TryGetValue(config.SplineKey, out var spline) || !spline.IsCreated)
                    {
                        continue;
                    }

                    var length = math.max(spline.Value.Length, 1e-3f);
                    var delta = config.Traversal == SplineTraversal.ConstantSpeed
                        ? config.Speed * DeltaTime / length
                        : DeltaTime / math.max(config.TraversalSeconds, 1e-3f);

                    state.Progress += delta;

                    var aimT = WrapEval(state.Progress + config.Lead, config.Wrap);
                    var targetPos = spline.Value.EvaluatePosition(aimT);

                    var selfPos = PhysicsMath.ResolvePosition(body, in LocalTransformLookup, in LocalToWorldLookup,
                        in ParentLookup);
                    var error = targetPos - selfPos;

                    PhysicsMath.ComputePidForce(error, config.Tuning, state.Pid, DeltaTime,
                        out var force, out var nextPid);

                    var targets = TargetsLookup.TryGetComponent(body, out var t) ? t : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.StrengthStat, body, targets,
                        LinkSources, Links, StatLookup);

                    force *= config.Strength * multiplier;

                    if (math.lengthsq(force) > 1e-5f)
                    {
                        pendingForces[i].Add(new PendingForce
                        {
                            Linear = force * DeltaTime,
                            Angular = float3.zero
                        });
                    }

                    state.Pid = nextPid;
                    states[i] = state;
                }
            }
        }
    }
}
