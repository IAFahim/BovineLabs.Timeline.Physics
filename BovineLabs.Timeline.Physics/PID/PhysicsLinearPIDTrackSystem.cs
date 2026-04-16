// BovineLabs.Timeline.Physics/PID/PhysicsLinearPIDTrackSystem.cs
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
        private ComponentLookup<PhysicsLinearPIDState> stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.blendImpl.OnCreate(ref state);
            this.activePidLookup = state.GetUnsafeComponentLookup<ActiveLinearPid>();
            this.stateLookup = state.GetComponentLookup<PhysicsLinearPIDState>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => this.blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            this.activePidLookup.Update(ref state);
            this.stateLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
            
            state.Dependency = new DisableStaleJob
            {
                ActiveLookup = this.activePidLookup
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ResetStateJob
            {
                StateLookup = this.stateLookup
            }.ScheduleParallel(state.Dependency);
            
            var blendData = this.blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActivePidLookup = this.activePidLookup
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
            public ComponentLookup<PhysicsLinearPIDState> StateLookup;

            private void Execute(in TrackBinding binding)
            {
                if (this.StateLookup.HasComponent(binding.Value))
                {
                    this.StateLookup.GetRefRW(binding.Value).ValueRW.State = default;
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
                if (this.ActiveLookup.HasComponent(binding.Value))
                {
                    this.ActiveLookup.SetComponentEnabled(binding.Value, false);
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
                this.Read(this.BlendData, entryIndex, out var entity, out var mixData);
                if (!this.ActivePidLookup.HasComponent(entity)) return;

                this.ActivePidLookup.SetComponentEnabled(entity, true);
                this.ActivePidLookup[entity] = new ActiveLinearPid
                {
                    Config = JobHelpers.Blend<PhysicsLinearPIDData, PhysicsLinearPIDMixer>(ref mixData, default)
                };
            }
        }
    }
}