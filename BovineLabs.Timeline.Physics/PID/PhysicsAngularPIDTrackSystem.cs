// BovineLabs.Timeline.Physics/PID/PhysicsAngularPIDTrackSystem.cs
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
        private TrackBlendImpl<PhysicsAngularPIDData, PhysicsAngularPIDAnimated> blendImpl;
        private UnsafeComponentLookup<ActiveAngularPid> activePidLookup;
        private ComponentLookup<PhysicsAngularPIDState> stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.blendImpl.OnCreate(ref state);
            this.activePidLookup = state.GetUnsafeComponentLookup<ActiveAngularPid>();
            this.stateLookup = state.GetComponentLookup<PhysicsAngularPIDState>();
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
            public UnsafeComponentLookup<ActiveAngularPid> ActiveLookup;

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
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsAngularPIDData>>.ReadOnly BlendData;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveAngularPid> ActivePidLookup;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(this.BlendData, entryIndex, out var entity, out var mixData);
                if (!this.ActivePidLookup.HasComponent(entity)) return;

                this.ActivePidLookup.SetComponentEnabled(entity, true);
                this.ActivePidLookup[entity] = new ActiveAngularPid
                {
                    Config = JobHelpers.Blend<PhysicsAngularPIDData, PhysicsAngularPIDMixer>(ref mixData, default)
                };
            }
        }
    }
}