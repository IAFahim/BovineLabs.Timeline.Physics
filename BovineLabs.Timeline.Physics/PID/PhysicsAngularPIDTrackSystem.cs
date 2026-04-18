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
    [UpdateAfter(typeof(PhysicsLinearPIDTrackSystem))]
    public partial struct PhysicsAngularPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsAngularPIDData, PhysicsAngularPIDAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveAngularPid> _activePidLookup;
        private ComponentLookup<PhysicsAngularPIDState> _stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activePidLookup = state.GetUnsafeComponentLookup<ActiveAngularPid>();
            _stateLookup = state.GetComponentLookup<PhysicsAngularPIDState>();
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
            private void Execute(ref PhysicsAngularPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithNone(typeof(ClipActivePrevious))]
        private partial struct ResetStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<PhysicsAngularPIDState> StateLookup;

            private void Execute(in TrackBinding binding)
            {
                if (StateLookup.HasComponent(binding.Value))
                {
                    StateLookup.GetRefRW(binding.Value).ValueRW.State = default;
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public UnsafeComponentLookup<ActiveAngularPid> ActiveLookup;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsAngularPIDData>>.ReadOnly BlendData;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveAngularPid> ActivePidLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActivePidLookup.HasComponent(entity)) return;

                ActivePidLookup.SetComponentEnabled(entity, true);
                ActivePidLookup[entity] = new ActiveAngularPid
                {
                    Config = JobHelpers.Blend<PhysicsAngularPIDData, PhysicsAngularPIDMixer>(ref mixData, default)
                };
            }
        }
    }
}