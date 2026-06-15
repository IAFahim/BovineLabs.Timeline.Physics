using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Kernels
{
    public struct TrackBlendDriver<TData, TAnimated, TActive, TMixer>
        where TData : unmanaged
        where TAnimated : unmanaged, IAnimatedComponent<TData>, IPreparable
        where TActive : unmanaged, IActive<TData>
        where TMixer : unmanaged, IMixer<TData>
    {
        private TrackBlendImpl<TData, TAnimated> _blendImpl;
        private ComponentLookup<TActive> _activeLookup;
        private ComponentTypeHandle<TAnimated> _animatedHandle;
        private ComponentTypeHandle<TrackBinding> _bindingHandle;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;
        private EntityQuery _ecbQuery;

        public ComponentLookup<TActive> ActiveLookup => _activeLookup;
        public ComponentTypeHandle<TrackBinding> BindingHandle => _bindingHandle;

        public void OnCreate(ref SystemState state)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<TActive>();
            _animatedHandle = state.GetComponentTypeHandle<TAnimated>();
            _bindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);

            using (var prepare = new EntityQueryBuilder(Allocator.Temp)
                       .WithAllRW<TAnimated>()
                       .WithAll<ClipActive>())
            {
                _prepareQuery = state.GetEntityQuery(prepare);
            }

            using (var stale = new EntityQueryBuilder(Allocator.Temp)
                       .WithAll<TrackBinding, ClipActivePrevious, TAnimated>()
                       .WithNone<ClipActive>())
            {
                _disableStaleQuery = state.GetEntityQuery(stale);
            }

            _ecbQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<BeginSimulationEntityCommandBufferSystem.Singleton>());
        }

        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
        }

        public void UpdateLookups(ref SystemState state)
        {
            _activeLookup.Update(ref state);
            _animatedHandle.Update(ref state);
            _bindingHandle.Update(ref state);
        }

        public void OnUpdate(ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
        {
            UpdateLookups(ref state);

            state.Dependency = new PrepareAnimatedJob<TData, TAnimated>
            {
                AnimatedHandle = _animatedHandle
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new DisableAbsentTrackJob<TData, TActive>
            {
                TrackBindingTypeHandle = _bindingHandle,
                BlendData = blendData,
                ActiveLookup = _activeLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            state.Dependency = new WriteActiveJob<TData, TActive, TMixer>
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                ECB = ecb
            }.ScheduleParallel(blendData, 64, state.Dependency);
        }
    }
}