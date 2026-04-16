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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            blendImpl.OnCreate(ref state);
            activePidLookup = state.GetUnsafeComponentLookup<ActiveAngularPid>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => blendImpl.OnDestroy(ref state);

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            activePidLookup.Update(ref state);

            state.Dependency = new PrepareJob().ScheduleParallel(state.Dependency);
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
            private void Execute(ref PhysicsAngularPIDAnimated animated) => animated.Value = animated.AuthoredData;
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsAngularPIDData>>.ReadOnly BlendData;
            public UnsafeComponentLookup<ActiveAngularPid> ActivePidLookup;

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