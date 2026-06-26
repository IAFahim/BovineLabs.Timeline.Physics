using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Shapes
{
    // Mirror of PhysicsShapeResizeTrackSystem; writes ActiveShapeSwap. PhysicsShapeSwapApplySystem re-points the
    // PhysicsCollider blob reference.
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsShapeSwapTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsShapeSwapData, PhysicsShapeSwapAnimated> _blendImpl;
        private ComponentLookup<ActiveShapeSwap> _activeLookup;
        private ComponentLookup<PhysicsShapeSwapState> _stateLookup;

        private EntityQuery _resetQuery;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<ActiveShapeSwap>();
            _stateLookup = state.GetComponentLookup<PhysicsShapeSwapState>();

            _resetQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsShapeSwapAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>()
                .Build();

            _prepareQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsShapeSwapAnimated>()
                .WithAll<ClipActive>()
                .Build();

            _disableStaleQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, ClipActivePrevious, PhysicsShapeSwapAnimated>()
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
            state.Dependency = new ResetStateTrackJob<PhysicsShapeSwapState, ActiveShapeSwap>
            {
                TrackBindingTypeHandle = bindingType,
                StateLookup = _stateLookup,
                ActiveLookup = _activeLookup,
                ResetValue = new PhysicsShapeSwapState { Fired = false }
            }.ScheduleParallel(_resetQuery, state.Dependency);

            var animatedType = SystemAPI.GetComponentTypeHandle<PhysicsShapeSwapAnimated>();
            state.Dependency = new PrepareJob
            {
                AnimatedTypeHandle = animatedType
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new DisableAbsentTrackJob<PhysicsShapeSwapData, ActiveShapeSwap>
            {
                TrackBindingTypeHandle = bindingType,
                BlendData = blendData,
                ActiveLookup = _activeLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                ECB = ecbWrite
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        private struct PrepareJob : IJobChunk
        {
            public ComponentTypeHandle<PhysicsShapeSwapAnimated> AnimatedTypeHandle;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsShapeSwapData>>.ReadOnly BlendData;
            [ReadOnly] public ComponentLookup<ActiveShapeSwap> ActiveLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveShapeSwap>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveShapeSwap
                {
                    Config = JobHelpers.Blend<PhysicsShapeSwapData, DiscreteMixer<PhysicsShapeSwapData>>(
                        ref mixData,
                        default)
                });
            }
        }
    }
}
