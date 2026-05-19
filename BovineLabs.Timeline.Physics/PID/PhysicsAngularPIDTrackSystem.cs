using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Core.Utility;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(TimelineComponentAnimationGroup))]
    [UpdateAfter(typeof(PhysicsLinearPIDTrackSystem))]
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    public partial struct PhysicsAngularPIDTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsAngularPIDData, PhysicsAngularPIDAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveAngularPid> _activePidLookup;
        private UnsafeComponentLookup<PhysicsAngularPIDState> _stateLookup;
        private EntityLock _stateLock;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activePidLookup = state.GetUnsafeComponentLookup<ActiveAngularPid>();
            _stateLookup = state.GetUnsafeComponentLookup<PhysicsAngularPIDState>();
            _stateLock = new EntityLock(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
            _stateLock.Dispose();
        }

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
                StateLookup = _stateLookup,
                EntityLock = _stateLock
            }.ScheduleParallel(state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActivePidLookup = _activePidLookup,
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct PrepareJob : IJobEntity
        {
            private void Execute(ref PhysicsAngularPIDAnimated animated)
            {
                animated.Value = animated.AuthoredData;
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        [WithNone(typeof(ClipActivePrevious))]
        private partial struct ResetStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<PhysicsAngularPIDState> StateLookup;
            public EntityLock EntityLock;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value == Entity.Null) return;
                using (EntityLock.Acquire(binding.Value))
                {
                    if (StateLookup.HasComponent(binding.Value))
                    {
                        var state = StateLookup[binding.Value];
                        state.State = default;
                        StateLookup[binding.Value] = state;
                    }
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveAngularPid> ActiveLookup;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value == Entity.Null) return;
                if (ActiveLookup.HasComponent(binding.Value)) ActiveLookup.SetComponentEnabled(binding.Value, false);
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsAngularPIDData>>.ReadOnly BlendData;
            [ReadOnly] public UnsafeComponentLookup<ActiveAngularPid> ActivePidLookup;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);
                if (!ActivePidLookup.HasComponent(entity)) return;

                ECB.SetComponentEnabled<ActiveAngularPid>(entryIndex, entity, true);
                ECB.SetComponent(entryIndex, entity, new ActiveAngularPid
                {
                    Config = JobHelpers.Blend<PhysicsAngularPIDData, PhysicsAngularPIDMixer>(ref mixData, default)
                });
            }
        }
    }
}