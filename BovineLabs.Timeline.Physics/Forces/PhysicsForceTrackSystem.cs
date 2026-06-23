using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Forces
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    public partial struct PhysicsForceTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsForceData, PhysicsForceAnimated> _blendImpl;
        private ComponentLookup<ActiveForce> _activeLookup;
        private ComponentLookup<PhysicsForceState> _stateLookup;

        private EntityQuery _resetQuery;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<ActiveForce>();
            _stateLookup = state.GetComponentLookup<PhysicsForceState>();

            _resetQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsForceAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>()
                .Build();

            _prepareQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsForceAnimated>()
                .WithAll<ClipActive>()
                .Build();

            _disableStaleQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, ClipActivePrevious, PhysicsForceAnimated>()
                .WithNone<ClipActive>()
                .Build();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeLookup.Update(ref state);
            _stateLookup.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

            var ecbWrite = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            state.Dependency = new ResetStateAlwaysTrackJob<PhysicsForceState>
            {
                TrackBindingTypeHandle = bindingType,
                StateLookup = _stateLookup,
                ResetValue = new PhysicsForceState { Fired = false }
            }.ScheduleParallel(_resetQuery, state.Dependency);

            var animatedType = SystemAPI.GetComponentTypeHandle<PhysicsForceAnimated>();
            state.Dependency = new PrepareForceDataJob
            {
                AnimatedTypeHandle = animatedType
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new DisableAbsentTrackJob<PhysicsForceData, ActiveForce>
            {
                TrackBindingTypeHandle = bindingType,
                BlendData = blendData,
                ActiveLookup = _activeLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                StateLookup = _stateLookup,
                DeltaTime = SystemAPI.Time.DeltaTime,
                ECB = ecbWrite
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        private struct PrepareForceDataJob : IJobChunk
        {
            public ComponentTypeHandle<PhysicsForceAnimated> AnimatedTypeHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var animateds = chunk.GetNativeArray(ref AnimatedTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var animated = animateds[i];
                    animated.Value = animated.AuthoredData;
                    animateds[i] = animated;
                }
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsForceData>>.ReadOnly BlendData;
            [ReadOnly] public ComponentLookup<ActiveForce> ActiveLookup;

            // Each body appears at most once in BlendData, so this per-body write never aliases.
            [NativeDisableParallelForRestriction] public ComponentLookup<PhysicsForceState> StateLookup;
            public float DeltaTime;

            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveForce>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveForce
                {
                    Config = JobHelpers.Blend<PhysicsForceData, PhysicsForceMixer>(ref mixData, default)
                });

                // Accumulate clip-active time on the body (render rate). The fixed-step consumer integrates the
                // Continuous force against the DELTA of this, so the total impulse = force × active-duration
                // independent of how many fixed steps run in the window. ResetStateAlwaysTrackJob zeroes the whole
                // state on the clip's first active frame, so this starts fresh each activation.
                if (StateLookup.HasComponent(entity))
                {
                    var s = StateLookup[entity];
                    s.ElapsedTime += DeltaTime;
                    StateLookup[entity] = s;
                }
            }
        }
    }
}