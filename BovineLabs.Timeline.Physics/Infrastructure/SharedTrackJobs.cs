using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    /// <summary>
    ///     Re-arms per-body track state at the start of a contiguous activation span, i.e. only when the
    ///     body's <c>Active*</c> component is currently disabled (no clip drove it last frame). Used by the
    ///     capture-and-restore override tracks (gravity, kinematic, collision-filter) whose state holds the
    ///     body's ORIGINAL value captured on enter: re-arming mid-span (e.g. across two touching clips on the
    ///     same body) would wipe that capture and let the apply system re-capture the already-overridden value
    ///     as the "original", so the gate is load-bearing. For fire-once / per-clip tracks that must re-fire on
    ///     every clip edge (force, velocity, PID, ricochet, teleport, …) use <see cref="ResetStateAlwaysTrackJob{TState}" />.
    /// </summary>
    [BurstCompile]
    public struct ResetStateTrackJob<TState, TActive> : IJobChunk
        where TState : unmanaged, IComponentData
        where TActive : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [NativeDisableParallelForRestriction] public ComponentLookup<TState> StateLookup;
        [ReadOnly] public ComponentLookup<TActive> ActiveLookup;
        public TState ResetValue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target == Entity.Null || !StateLookup.HasComponent(target)) continue;

                if (!ActiveLookup.HasComponent(target) || !ActiveLookup.IsComponentEnabled(target))
                    StateLookup[target] = ResetValue;
            }
        }
    }

    /// <summary>
    ///     Re-arms per-body track state on EVERY clip activation edge, unconditionally. Scheduled over the
    ///     "clip just became active" query (<c>WithAll&lt;ClipActive&gt;().WithNone&lt;ClipActivePrevious&gt;()</c>),
    ///     so each clip — including back-to-back/adjacent clips that share a target — resets the body's latch
    ///     (<c>Fired</c>, <c>ResetApplied</c>, direction/PID-integral state) and therefore re-fires. Use this for
    ///     fire-once / per-clip tracks. Capture-and-restore override tracks must NOT use it (see
    ///     <see cref="ResetStateTrackJob{TState,TActive}" />) — re-arming mid-span corrupts their captured original.
    /// </summary>
    [BurstCompile]
    public struct ResetStateAlwaysTrackJob<TState> : IJobChunk
        where TState : unmanaged, IComponentData
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [NativeDisableParallelForRestriction] public ComponentLookup<TState> StateLookup;
        public TState ResetValue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target == Entity.Null || !StateLookup.HasComponent(target)) continue;

                StateLookup[target] = ResetValue;
            }
        }
    }

    /// <summary>
    ///     Disables a body's <c>Active*</c> component once no clip of this track type drives it this frame,
    ///     i.e. when the body is absent from the current blend map. Scheduled over the "clip just ended"
    ///     query (<c>WithAll&lt;ClipActivePrevious&gt;().WithNone&lt;ClipActive&gt;()</c>) and run AFTER the
    ///     blend has been produced: an ended clip whose target is still present in <see cref="BlendData" />
    ///     (because another clip keeps driving it) is left enabled. This replaces the old timeline-end
    ///     disable, so while-active effects stop / restore at clip end rather than only when the whole
    ///     timeline deactivates.
    /// </summary>
    [BurstCompile]
    public struct DisableAbsentTrackJob<TData, TActive> : IJobChunk
        where TData : unmanaged
        where TActive : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [ReadOnly] public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly BlendData;
        [NativeDisableParallelForRestriction] public ComponentLookup<TActive> ActiveLookup;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target == Entity.Null) continue;
                if (!ActiveLookup.HasComponent(target)) continue;
                if (BlendData.ContainsKey(target)) continue;

                ActiveLookup.SetComponentEnabled(target, false);
            }
        }
    }

    [BurstCompile]
    public struct PrepareAnimatedJob<TData, TAnimated> : IJobChunk
        where TData : unmanaged
        where TAnimated : unmanaged, IAnimatedComponent<TData>, IPreparable
    {
        public ComponentTypeHandle<TAnimated> AnimatedHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var animateds = chunk.GetNativeArray(ref AnimatedHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var animated = animateds[i];
                animated.ResetToAuthored();
                animateds[i] = animated;
            }
        }
    }

    [BurstCompile]
    public struct WriteActiveJob<TData, TActive, TMixer> : IJobParallelHashMapDefer
        where TData : unmanaged
        where TActive : unmanaged, IActive<TData>
        where TMixer : unmanaged, IMixer<TData>
    {
        [ReadOnly] public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly BlendData;
        [ReadOnly] public ComponentLookup<TActive> ActiveLookup;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void ExecuteNext(int entryIndex, int jobIndex)
        {
            this.Read(BlendData, entryIndex, out var entity, out var mixData);
            if (!ActiveLookup.HasComponent(entity)) return;

            ECB.SetComponentEnabled<TActive>(entryIndex, entity, true);

            var active = default(TActive);
            active.Config = JobHelpers.Blend<TData, TMixer>(ref mixData, default);
            ECB.SetComponent(entryIndex, entity, active);
        }
    }
}