using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Data.Mixers;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kernels;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.VelocityOverrides
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsVelocityTrackSystem : ISystem
    {
        private TrackBlendDriver<PhysicsVelocityData, PhysicsVelocityAnimated, ActiveVelocity, PhysicsVelocityMixer>
            _driver;

        private ComponentLookup<PhysicsVelocityState> _stateLookup;
        private ComponentTypeHandle<TrackBinding> _bindingHandle;
        private EntityQuery _resetQuery;
        private EntityQuery _activeQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _driver.OnCreate(ref state);

            // ponytail: skip the whole system when there are no velocity-track entities.
            // Without this it schedules the generic PrepareAnimatedJob every frame; in an
            // IL2CPP player that schedule SIGSEGVs (missing job-reflection data for the
            // generic instantiation), killing the player before anything renders. A scene
            // with no PhysicsVelocityAnimated has no work here anyway.
            state.RequireForUpdate<PhysicsVelocityAnimated>();

            _stateLookup = state.GetComponentLookup<PhysicsVelocityState>();
            _bindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);

            using var reset = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, PhysicsVelocityAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>();
            _resetQuery = state.GetEntityQuery(reset);

            using var active = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, PhysicsVelocityAnimated, ClipActive>();
            _activeQuery = state.GetEntityQuery(active);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _driver.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            _driver.UpdateLookups(ref state);
            _stateLookup.Update(ref state);
            _bindingHandle.Update(ref state);

            state.Dependency = new ResetStateAlwaysTrackJob<PhysicsVelocityState>
            {
                TrackBindingTypeHandle = _driver.BindingHandle,
                StateLookup = _stateLookup,
                ResetValue = new PhysicsVelocityState { Fired = false }
            }.ScheduleParallel(_resetQuery, state.Dependency);

            // Accumulate render-rate clip-active time per body so the fixed-step AddContinuous integrator can deliver
            // velocity against the elapsed delta instead of the fixed dt (determinism — see PhysicsVelocityState).
            // Runs after the first-frame reset zeroes it. Single-threaded: the += would race two clips sharing a body.
            state.Dependency = new AccumulateElapsedJob
            {
                BindingHandle = _bindingHandle,
                StateLookup = _stateLookup,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.Schedule(_activeQuery, state.Dependency);

            _driver.OnUpdate(ref state, ecb);
        }

        [BurstCompile]
        private struct AccumulateElapsedJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<TrackBinding> BindingHandle;
            public ComponentLookup<PhysicsVelocityState> StateLookup;
            public float DeltaTime;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var bindings = chunk.GetNativeArray(ref BindingHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var target = bindings[i].Value;
                    // ponytail: two velocity clips on one body double-count elapsed here; acceptable until overlapping
                    // AddContinuous crossfades matter (then move accumulation onto the per-body blend result).
                    if (target != Entity.Null && StateLookup.HasComponent(target))
                    {
                        var s = StateLookup[target];
                        s.ElapsedTime += DeltaTime;
                        StateLookup[target] = s;
                    }
                }
            }
        }
    }
}