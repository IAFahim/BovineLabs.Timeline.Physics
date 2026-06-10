using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Kinematics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsKinematicOverrideTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsKinematicOverrideData, PhysicsKinematicOverrideAnimated> _blendImpl;
        private ComponentLookup<ActiveKinematicOverride> _activeLookup;
        private ComponentLookup<PhysicsKinematicOverrideState> _stateLookup;

        private EntityQuery _resetQuery;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<ActiveKinematicOverride>();
            _stateLookup = state.GetComponentLookup<PhysicsKinematicOverrideState>();

            _resetQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsKinematicOverrideAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>()
                .Build();

            _prepareQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsKinematicOverrideAnimated>()
                .WithAll<ClipActive>()
                .Build();

            _disableStaleQuery = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, TimelineActivePrevious, PhysicsKinematicOverrideAnimated>()
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
            _activeLookup.Update(ref state);
            _stateLookup.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();

            var ecbWrite = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var bindingType = SystemAPI.GetComponentTypeHandle<TrackBinding>(true);
            state.Dependency = new ResetStateTrackJob<PhysicsKinematicOverrideState, ActiveKinematicOverride>
            {
                TrackBindingTypeHandle = bindingType,
                StateLookup = _stateLookup,
                ActiveLookup = _activeLookup,
                ResetValue = new PhysicsKinematicOverrideState
                {
                    Fired = false, AddedGravityComponent = false, AddedMassOverrideComponent = false,
                    OriginalGravityScale = 1f, OriginalIsKinematic = 0
                }
            }.ScheduleParallel(_resetQuery, state.Dependency);

            var animatedType = SystemAPI.GetComponentTypeHandle<PhysicsKinematicOverrideAnimated>();
            state.Dependency = new PrepareJob
            {
                AnimatedTypeHandle = animatedType
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            state.Dependency = new DisableStaleTrackJob<ActiveKinematicOverride>
            {
                TrackBindingTypeHandle = bindingType,
                ActiveLookup = _activeLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

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
            public ComponentTypeHandle<PhysicsKinematicOverrideAnimated> AnimatedTypeHandle;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsKinematicOverrideData>>.ReadOnly BlendData;
            [ReadOnly] public ComponentLookup<ActiveKinematicOverride> ActiveLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveKinematicOverride>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveKinematicOverride
                {
                    Config = JobHelpers.Blend<PhysicsKinematicOverrideData, DiscreteMixer<PhysicsKinematicOverrideData>>(ref mixData,
                        default)
                });
            }
        }
    }
}