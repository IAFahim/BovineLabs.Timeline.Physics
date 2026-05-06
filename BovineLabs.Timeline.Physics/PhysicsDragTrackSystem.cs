using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    public partial struct PhysicsDragTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsDragData, PhysicsDragAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveDrag> _activeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetUnsafeComponentLookup<ActiveDrag>();
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

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);

            state.Dependency = new DisableStaleJob
            {
                ActiveLookup = _activeLookup
            }.ScheduleParallel(state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsDragAnimated animated)
            {
                animated.Value = animated.AuthoredData;
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveDrag> ActiveLookup;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value == Entity.Null) return;
                if (ActiveLookup.HasComponent(binding.Value)) ActiveLookup.SetComponentEnabled(binding.Value, false);
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsDragData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<ActiveDrag> ActiveLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveDrag>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveDrag
                {
                    Config = JobHelpers.Blend<PhysicsDragData, PhysicsDragMixer>(ref mixData, default)
                });
            }
        }
    }
}