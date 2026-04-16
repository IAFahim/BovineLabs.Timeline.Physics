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
    public partial struct PhysicsLinearPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsLinearPIDData, PhysicsLinearPIDAnimated> blendImpl;
        private UnsafeComponentLookup<ActiveLinearPid> activePidLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            activePidLookup = state.GetUnsafeComponentLookup<ActiveLinearPid>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            activePidLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            state.Dependency = new DisableStaleJob().ScheduleParallel(state.Dependency);
            
            var blendData = blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActivePidLookup = activePidLookup
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsLinearPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            private void Execute(in TrackBinding binding, ref PhysicsLinearPIDAnimated anim)
            {
                // Disable if timeline stops
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsLinearPIDData>>.ReadOnly BlendData;
            public UnsafeComponentLookup<ActiveLinearPid> ActivePidLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActivePidLookup.HasComponent(entity)) return;

                ActivePidLookup.SetComponentEnabled(entity, true);
                ActivePidLookup[entity] = new ActiveLinearPid
                {
                    Config = JobHelpers.Blend<PhysicsLinearPIDData, PhysicsLinearPIDMixer>(ref mixData, default)
                };
            }
        }
    }
}