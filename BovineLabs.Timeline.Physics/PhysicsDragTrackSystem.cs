// BovineLabs.Timeline.Physics/PhysicsDragTrackSystem.cs
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
            this.blendImpl.OnCreate(ref state);
            this.activeLookup = state.GetUnsafeComponentLookup<ActiveDrag>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => this.blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this.activeLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            
            state.Dependency = new DisableStaleJob
            {
                ActiveLookup = this.activeLookup
            }.ScheduleParallel(state.Dependency);
            
            var blendData = this.blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = this.activeLookup
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
            [NativeDisableParallelForRestriction]
            public UnsafeComponentLookup<ActiveDrag> ActiveLookup;

            private void Execute(in TrackBinding binding)
            {
                if (this.ActiveLookup.HasComponent(binding.Value))
                {
                    this.ActiveLookup.SetComponentEnabled(binding.Value, false);
                }
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsDragData>>.ReadOnly BlendData;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveDrag> ActiveLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(this.BlendData, entryIndex, out var entity, out var mixData);
                if (!this.ActiveLookup.HasComponent(entity)) return;

                this.ActiveLookup.SetComponentEnabled(entity, true);
                this.ActiveLookup[entity] = new ActiveDrag
                {
                    Config = JobHelpers.Blend<PhysicsDragData, PhysicsDragMixer>(ref mixData, default)
                };
            }
        }
    }
}