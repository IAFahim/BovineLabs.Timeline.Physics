using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Splines
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    public partial struct PhysicsSplineFollowTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsSplineFollowData, PhysicsSplineFollowAnimated> _blendImpl;
        private ComponentLookup<ActiveSplineFollow> _activeLookup;
        private ComponentLookup<PhysicsSplineFollowState> _stateLookup;

        private EntityQuery _resetQuery;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<ActiveSplineFollow>();
            _stateLookup = state.GetComponentLookup<PhysicsSplineFollowState>();

            _resetQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsSplineFollowAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>()
                .Build();

            _prepareQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsSplineFollowAnimated>()
                .WithAll<ClipActive>()
                .Build();

            _disableStaleQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, ClipActivePrevious, PhysicsSplineFollowAnimated>()
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
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            state.Dependency = new ResetStateAlwaysTrackJob<PhysicsSplineFollowState>
            {
                TrackBindingTypeHandle = bindingType,
                StateLookup = _stateLookup,
                ResetValue = default
            }.ScheduleParallel(_resetQuery, state.Dependency);

            var animatedType = SystemAPI.GetComponentTypeHandle<PhysicsSplineFollowAnimated>();
            state.Dependency = new PrepareJob
            {
                AnimatedTypeHandle = animatedType
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new DisableAbsentTrackJob<PhysicsSplineFollowData, ActiveSplineFollow>
            {
                TrackBindingTypeHandle = bindingType,
                BlendData = blendData,
                ActiveLookup = _activeLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                ECB = ecb
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        private struct PrepareJob : IJobChunk
        {
            public ComponentTypeHandle<PhysicsSplineFollowAnimated> AnimatedTypeHandle;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsSplineFollowData>>.ReadOnly BlendData;
            [ReadOnly] public ComponentLookup<ActiveSplineFollow> ActiveLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveSplineFollow>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveSplineFollow
                {
                    Config = JobHelpers.Blend<PhysicsSplineFollowData, PhysicsSplineFollowMixer>(ref mixData, default)
                });
            }
        }
    }
}