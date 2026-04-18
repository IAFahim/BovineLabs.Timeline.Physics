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
        private TrackBlendImpl<PhysicsLinearPIDData, PhysicsLinearPIDAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveLinearPid> _activePidLookup;
        private UnsafeComponentLookup<PhysicsLinearPIDState> _stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activePidLookup = state.GetUnsafeComponentLookup<ActiveLinearPid>();
            _stateLookup = state.GetUnsafeComponentLookup<PhysicsLinearPIDState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activePidLookup.Update(ref state);
            _stateLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            
            state.Dependency = new DisableStaleJob
            {
                ActiveLookup = _activePidLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ResetStateJob
            {
                StateLookup = _stateLookup
            }.ScheduleParallel(state.Dependency);
            
            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActivePidLookup = _activePidLookup
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsLinearPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithNone(typeof(ClipActivePrevious))]
        private partial struct ResetStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public UnsafeComponentLookup<PhysicsLinearPIDState> StateLookup;

            private void Execute(in TrackBinding binding)
            {
                if (StateLookup.HasComponent(binding.Value))
                {
                    var state = StateLookup[binding.Value];
                    state.State = default;
                    StateLookup[binding.Value] = state;
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public UnsafeComponentLookup<ActiveLinearPid> ActiveLookup;

            private void Execute(in TrackBinding binding)
            {
                if (ActiveLookup.HasComponent(binding.Value))
                {
                    ActiveLookup.SetComponentEnabled(binding.Value, false);
                }
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsLinearPIDData>>.ReadOnly BlendData;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveLinearPid> ActivePidLookup;

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
