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
    public partial struct PhysicsForceTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsForceData, PhysicsForceAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveForce> _activeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetUnsafeComponentLookup<ActiveForce>();
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

            state.Dependency = new ResetStateJob
            {
                StateLookup = state.GetUnsafeComponentLookup<PhysicsForceState>()
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new PrepareForceDataJob().ScheduleParallel(state.Dependency);

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
        private partial struct PrepareForceDataJob : IJobEntity
        {
            private void Execute(ref PhysicsForceAnimated animated)
            {
                animated.Value = animated.AuthoredData;
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveForce> ActiveLookup;

            private void Execute(in TrackBinding binding)
            {
                if (ActiveLookup.HasComponent(binding.Value)) ActiveLookup.SetComponentEnabled(binding.Value, false);
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithNone(typeof(ClipActivePrevious))]
        private partial struct ResetStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<PhysicsForceState> StateLookup;

            private void Execute(in TrackBinding binding)
            {
                if (StateLookup.HasComponent(binding.Value))
                {
                    var s = StateLookup[binding.Value];
                    s.Fired = false;
                    StateLookup[binding.Value] = s;
                }
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsForceData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<ActiveForce> ActiveLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActiveLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveForce>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveForce
                {
                    Config = JobHelpers.Blend<PhysicsForceData, PhysicsForceMixer>(ref mixData, default)
                });
            }
        }
    }
}