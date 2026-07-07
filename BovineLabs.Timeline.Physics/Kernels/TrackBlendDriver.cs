using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Kernels
{
    /// <summary>
    /// When the per-body state re-arms. This used to be an implicit choice of which reset job a hand-rolled
    /// system happened to copy; making it a parameter keeps the fire-once vs capture-restore lifecycle reviewable.
    /// </summary>
    public enum RearmPolicy : byte
    {
        /// <summary>No per-body state to reset (stateless tracks).</summary>
        None,

        /// <summary>Reset on every clip activation, even mid-span — adjacent fire-once clips each fire.</summary>
        EveryActivation,

        /// <summary>Reset only at a true span start (Active disabled) — capture-restore overrides keep their
        /// captured original across touching clips instead of re-capturing the overridden value. Served by
        /// <see cref="TrackBlendRestorableStateDriver{TData,TAnimated,TActive,TMixer,TState}"/>, because the
        /// span-start reset is constrained to <see cref="IRestorableState"/> (which the fire-once states do not
        /// implement) so a zero-fixed-tick gap never wipes a still-pending exit restore.</summary>
        SpanStart,
    }

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

            state.RequireForUpdate<TAnimated>();
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

        public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly OnUpdate(
            ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
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

            return blendData;
        }
    }

    /// <summary>
    /// <see cref="TrackBlendDriver{TData,TAnimated,TActive,TMixer}"/> plus per-body state reset. This is the
    /// full lifecycle every physics track system shares; a system shell is the driver field, an OnCreate call
    /// with its <see cref="RearmPolicy"/>, and an OnUpdate call.
    /// </summary>
    public struct TrackBlendStateDriver<TData, TAnimated, TActive, TMixer, TState>
        where TData : unmanaged
        where TAnimated : unmanaged, IAnimatedComponent<TData>, IPreparable
        where TActive : unmanaged, IActive<TData>
        where TMixer : unmanaged, IMixer<TData>
        where TState : unmanaged, IComponentData
    {
        private TrackBlendDriver<TData, TAnimated, TActive, TMixer> _driver;
        private ComponentLookup<TState> _stateLookup;
        private EntityQuery _resetQuery;
        private RearmPolicy _policy;
        private TState _resetValue;

        public ComponentLookup<TState> StateLookup => _stateLookup;

        public void OnCreate(ref SystemState state, RearmPolicy policy, TState resetValue = default)
        {
            _driver.OnCreate(ref state);
            _stateLookup = state.GetComponentLookup<TState>();
            _policy = policy;
            _resetValue = resetValue;

            using var reset = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, TAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>();
            _resetQuery = state.GetEntityQuery(reset);
        }

        public void OnDestroy(ref SystemState state)
        {
            _driver.OnDestroy(ref state);
        }

        public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly OnUpdate(
            ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
        {
            _stateLookup.Update(ref state);
            _driver.UpdateLookups(ref state);

            switch (_policy)
            {
                case RearmPolicy.EveryActivation:
                    state.Dependency = new ResetStateAlwaysTrackJob<TState>
                    {
                        TrackBindingTypeHandle = _driver.BindingHandle,
                        StateLookup = _stateLookup,
                        ResetValue = _resetValue
                    }.ScheduleParallel(_resetQuery, state.Dependency);
                    break;

                // RearmPolicy.SpanStart is served by TrackBlendRestorableStateDriver — its reset is constrained to
                // IRestorableState so a zero-fixed-tick clip gap cannot wipe a pending exit restore.
            }

            return _driver.OnUpdate(ref state, ecb);
        }
    }

    /// <summary>
    /// <see cref="TrackBlendStateDriver{TData,TAnimated,TActive,TMixer,TState}"/> for the fire-once / continuous-motion
    /// families whose effect is consumed on the fixed-step clock (Force, Velocity, PID, Teleport). Identical render-side
    /// pipeline (EveryActivation re-arm, prepare, blend, write) but the stale-disable is the drain-aware
    /// <see cref="DisableAbsentDrainableTrackJob{TData,TActive,TState}"/>: instead of dropping a latch the moment its
    /// clip ends (which loses a short clip whose window straddled no fixed tick, and truncates a continuous tail), it
    /// lingers an undrained latch enabled so the fixed clock is guaranteed one apply tick to service it. Pairs with the
    /// fixed-step <c>PhysicsLatchDrainFinalizeSystem</c>, which disables the serviced orphan. Split from
    /// <see cref="TrackBlendStateDriver{TData,TAnimated,TActive,TMixer,TState}"/> because the disable job it schedules is
    /// constrained to <see cref="IDrainableLatchState{TData}"/>, which the other EveryActivation states do not implement.
    /// </summary>
    public struct TrackBlendDrainableStateDriver<TData, TAnimated, TActive, TMixer, TState>
        where TData : unmanaged
        where TAnimated : unmanaged, IAnimatedComponent<TData>, IPreparable
        where TActive : unmanaged, IActive<TData>
        where TMixer : unmanaged, IMixer<TData>
        where TState : unmanaged, IComponentData, IDrainableLatchState<TData>
    {
        private TrackBlendImpl<TData, TAnimated> _blendImpl;
        private ComponentLookup<TActive> _activeLookup;
        private ComponentLookup<TState> _stateLookup;
        private ComponentTypeHandle<TAnimated> _animatedHandle;
        private ComponentTypeHandle<TrackBinding> _bindingHandle;
        private EntityQuery _prepareQuery;
        private EntityQuery _disableStaleQuery;
        private EntityQuery _resetQuery;
        private TState _resetValue;

        public ComponentLookup<TState> StateLookup => _stateLookup;

        public void OnCreate(ref SystemState state, TState resetValue = default)
        {
            _blendImpl.OnCreate(ref state);
            _activeLookup = state.GetComponentLookup<TActive>();
            _stateLookup = state.GetComponentLookup<TState>();
            _animatedHandle = state.GetComponentTypeHandle<TAnimated>();
            _bindingHandle = state.GetComponentTypeHandle<TrackBinding>(true);
            _resetValue = resetValue;

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

            using (var reset = new EntityQueryBuilder(Allocator.Temp)
                       .WithAll<TrackBinding, TAnimated, ClipActive>()
                       .WithNone<ClipActivePrevious>())
            {
                _resetQuery = state.GetEntityQuery(reset);
            }

            state.RequireForUpdate<TAnimated>();
        }

        public void OnDestroy(ref SystemState state)
        {
            _blendImpl.OnDestroy(ref state);
        }

        public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly OnUpdate(
            ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
        {
            _activeLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _animatedHandle.Update(ref state);
            _bindingHandle.Update(ref state);

            // EveryActivation re-arm (also scrubs the Orphaned linger flag via the default-ish ResetValue).
            state.Dependency = new ResetStateAlwaysTrackJob<TState>
            {
                TrackBindingTypeHandle = _bindingHandle,
                StateLookup = _stateLookup,
                ResetValue = _resetValue
            }.ScheduleParallel(_resetQuery, state.Dependency);

            state.Dependency = new PrepareAnimatedJob<TData, TAnimated>
            {
                AnimatedHandle = _animatedHandle
            }.ScheduleParallel(_prepareQuery, state.Dependency);

            var blendData = _blendImpl.Update(ref state);

            state.Dependency = new DisableAbsentDrainableTrackJob<TData, TActive, TState>
            {
                TrackBindingTypeHandle = _bindingHandle,
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                StateLookup = _stateLookup
            }.ScheduleParallel(_disableStaleQuery, state.Dependency);

            state.Dependency = new WriteActiveJob<TData, TActive, TMixer>
            {
                BlendData = blendData,
                ActiveLookup = _activeLookup,
                ECB = ecb
            }.ScheduleParallel(blendData, 64, state.Dependency);

            return blendData;
        }
    }

    /// <summary>
    /// <see cref="TrackBlendStateDriver{TData,TAnimated,TActive,TMixer,TState}"/> for the capture-restore override
    /// tracks (gravity/kinematic/filter/shape swap+resize): per-body state is re-armed only at a true span start
    /// (Active disabled) AND never while a prior exit restore is still pending. Split from the general state driver
    /// because the <see cref="ResetStateTrackJob{TState,TActive}"/> it schedules is constrained to
    /// <see cref="IRestorableState"/>, which the fire-once (EveryActivation) states do not implement.
    /// </summary>
    public struct TrackBlendRestorableStateDriver<TData, TAnimated, TActive, TMixer, TState>
        where TData : unmanaged
        where TAnimated : unmanaged, IAnimatedComponent<TData>, IPreparable
        where TActive : unmanaged, IActive<TData>
        where TMixer : unmanaged, IMixer<TData>
        where TState : unmanaged, IComponentData, IRestorableState
    {
        private TrackBlendDriver<TData, TAnimated, TActive, TMixer> _driver;
        private ComponentLookup<TState> _stateLookup;
        private EntityQuery _resetQuery;
        private TState _resetValue;

        public ComponentLookup<TState> StateLookup => _stateLookup;

        public void OnCreate(ref SystemState state, TState resetValue = default)
        {
            _driver.OnCreate(ref state);
            _stateLookup = state.GetComponentLookup<TState>();
            _resetValue = resetValue;

            using var reset = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TrackBinding, TAnimated, ClipActive>()
                .WithNone<ClipActivePrevious>();
            _resetQuery = state.GetEntityQuery(reset);
        }

        public void OnDestroy(ref SystemState state)
        {
            _driver.OnDestroy(ref state);
        }

        public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly OnUpdate(
            ref SystemState state, EntityCommandBuffer.ParallelWriter ecb)
        {
            _stateLookup.Update(ref state);
            _driver.UpdateLookups(ref state);

            state.Dependency = new ResetStateTrackJob<TState, TActive>
            {
                TrackBindingTypeHandle = _driver.BindingHandle,
                StateLookup = _stateLookup,
                ActiveLookup = _driver.ActiveLookup,
                ResetValue = _resetValue
            }.ScheduleParallel(_resetQuery, state.Dependency);

            return _driver.OnUpdate(ref state, ecb);
        }
    }
}
