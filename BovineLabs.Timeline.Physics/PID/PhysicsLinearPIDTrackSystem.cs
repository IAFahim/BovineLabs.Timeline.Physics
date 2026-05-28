using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsLinearPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsLinearPIDData, PhysicsLinearPIDAnimated> _blendImpl;
        private ComponentLookup<ActiveLinearPid> _activePidLookup;
        private ComponentLookup<PhysicsLinearPIDState> _stateLookup;

        private EntityQuery _resetQuery;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activePidLookup = state.GetComponentLookup<ActiveLinearPid>(false);
            _stateLookup = state.GetComponentLookup<PhysicsLinearPIDState>(false);

            _resetQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsLinearPIDAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>()
                .Build();

            _prepareQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsLinearPIDAnimated>()
                .WithAll<ClipActive>()
                .Build();

            _disableStaleQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, TimelineActivePrevious, PhysicsLinearPIDAnimated>()
                .WithNone<TimelineActive>()
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
            _activePidLookup.Update(ref state);
            _stateLookup.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            state.Dependency = new ResetStateTrackJob<PhysicsLinearPIDState>
            {
                TrackBindingTypeHandle = bindingType,
                StateLookup = _stateLookup,
                ResetValue = default
            }.ScheduleParallel(_resetQuery, state.Dependency);

            var animatedType = SystemAPI.GetComponentTypeHandle<PhysicsLinearPIDAnimated>();
            state.Dependency = new PrepareJob
            {
                AnimatedTypeHandle = animatedType
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            state.Dependency = new DisableStaleTrackJob<ActiveLinearPid>
            {
                TrackBindingTypeHandle = bindingType,
                ActiveLookup = _activePidLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActivePidLookup = _activePidLookup,
                ECB = ecb
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        private struct PrepareJob : IJobChunk
        {
            public ComponentTypeHandle<PhysicsLinearPIDAnimated> AnimatedTypeHandle;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsLinearPIDData>>.ReadOnly BlendData;
            [ReadOnly] public ComponentLookup<ActiveLinearPid> ActivePidLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActivePidLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveLinearPid>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveLinearPid
                {
                    Config = JobHelpers.Blend<PhysicsLinearPIDData, PhysicsLinearPIDMixer>(ref mixData, default)
                });
            }
        }
    }
}