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
    [UpdateAfter(typeof(EntityLinkTargetPatchSystem))]
    public partial struct PhysicsForceTrackSystem : ISystem
    {
        private TrackBlendImpl<PhysicsForceData, PhysicsForceAnimated> _blendImpl;
        private UnsafeComponentLookup<ActiveForce> _activeLookup;
        private UnsafeComponentLookup<PhysicsForceState> _stateLookup;
        private EntityLock _entityLock;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetUnsafeComponentLookup<ActiveForce>();
            _stateLookup = state.GetUnsafeComponentLookup<PhysicsForceState>();
            _entityLock = new EntityLock(Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
            _entityLock.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeLookup.Update(ref state);
            _stateLookup.Update(ref state);

            state.Dependency = new ResetStateJob
            {
                StateLookup = _stateLookup,
                EntityLock = _entityLock
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new PrepareForceDataJob().ScheduleParallel(state.Dependency);

            state.Dependency = new DisableStaleJob
            {
                ActiveLookup = _activeLookup,
                EntityLock = _entityLock
            }.ScheduleParallel(state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new WriteActiveJob
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                EntityLock = _entityLock
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
        [WithAll(typeof(ClipActive))]
        [WithNone(typeof(ClipActivePrevious))]
        private partial struct ResetStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<PhysicsForceState> StateLookup;
            public EntityLock EntityLock;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value == Entity.Null) return;
                using (EntityLock.Acquire(binding.Value))
                {
                    if (StateLookup.HasComponent(binding.Value))
                    {
                        var s = StateLookup[binding.Value];
                        s.Fired = false;
                        StateLookup[binding.Value] = s;
                    }
                }
            }
        }

        [BurstCompile]
        [WithNone(typeof(TimelineActive))]
        [WithAll(typeof(TimelineActivePrevious))]
        private partial struct DisableStaleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveForce> ActiveLookup;
            public EntityLock EntityLock;

            private void Execute(in TrackBinding binding)
            {
                if (binding.Value == Entity.Null) return;
                using (EntityLock.Acquire(binding.Value))
                {
                    if (ActiveLookup.HasComponent(binding.Value))
                        ActiveLookup.SetComponentEnabled(binding.Value, false);
                }
            }
        }

        [BurstCompile]
        private struct WriteActiveJob : IJobParallelHashMapDefer
        {
            [ReadOnly] public NativeParallelHashMap<Entity, MixData<PhysicsForceData>>.ReadOnly BlendData;
            [NativeDisableParallelForRestriction] public UnsafeComponentLookup<ActiveForce> ActiveLookup;
            public EntityLock EntityLock;

            public void ExecuteNext(int entryIndex, int jobIndex)
            {
                this.Read(BlendData, entryIndex, out var entity, out var mixData);

                using (EntityLock.Acquire(entity))
                {
                    if (!ActiveLookup.HasComponent(entity)) return;

                    ActiveLookup.SetComponentEnabled(entity, true);
                    ActiveLookup[entity] = new ActiveForce
                    {
                        Config = JobHelpers.Blend<PhysicsForceData, PhysicsForceMixer>(ref mixData, default)
                    };
                }
            }
        }
    }
}