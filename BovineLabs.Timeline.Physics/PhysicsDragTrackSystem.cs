using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    public partial struct PhysicsDragTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsDragData, PhysicsDragAnimated> blendImpl;
        private UnsafeComponentLookup<ActiveDrag> activeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            activeLookup = state.GetUnsafeComponentLookup<ActiveDrag>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            activeLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            state.Dependency = new DisableStaleJob().ScheduleParallel(state.Dependency);
            
            var blendData = blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = activeLookup
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsDragAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            private void Execute(in TrackBinding binding, ref PhysicsDragAnimated anim) { }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsDragData>>.ReadOnly BlendData;
            public UnsafeComponentLookup<ActiveDrag> ActiveLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ActiveLookup.SetComponentEnabled(entity, true);
                ActiveLookup[entity] = new ActiveDrag
                {
                    Config = JobHelpers.Blend<PhysicsDragData, PhysicsDragMixer>(ref mixData, default)
                };
            }
        }
    }
}
