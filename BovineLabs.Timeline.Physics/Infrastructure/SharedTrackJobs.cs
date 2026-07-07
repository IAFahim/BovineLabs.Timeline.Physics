using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    [BurstCompile]
    public struct ResetStateTrackJob<TState, TActive> : IJobChunk
        where TState : unmanaged, IComponentData, IRestorableState
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

                if (ActiveLookup.HasComponent(target) && ActiveLookup.IsComponentEnabled(target)) continue;

                // Active is disabled: a true span start. But if the state still owes an exit restore (its OnExit
                // hasn't run — a clip gap with zero fixed ticks between the old clip's disable and this new clip's
                // reset), keep the captured original intact. Wiping it here would strand the override forever: the
                // next OnEnter would re-capture the already-overridden value as "original" (gravity stuck at the
                // override, body stuck kinematic, swapped collider blob lost). The eventual real exit restores it,
                // and until then OnStay keeps overriding with the original still captured (the touching-clips case).
                if (StateLookup[target].RestorePending) continue;

                StateLookup[target] = ResetValue;
            }
        }
    }

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

    [BurstCompile]
    public struct DisableAbsentTrackJob<TData, TActive> : IJobChunk
        where TData : unmanaged
        where TActive : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [ReadOnly] public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly BlendData;
        [NativeDisableParallelForRestriction] public ComponentLookup<TActive> ActiveLookup;

        public JobHandle ScheduleParallel(EntityQuery query, JobHandle dependsOn)
        {
            return this.Schedule(query, dependsOn);
        }

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

    /// <summary>
    /// Drain-aware variant of <see cref="DisableAbsentTrackJob{TData,TActive}"/> for the fire-once / continuous-motion
    /// latch families (Force, Velocity, PID, Teleport). Same stale-disable role, but it must not drop a latch that
    /// still owes the fixed clock work: the render-rate enable is delayed one frame (next-frame ECB) and the disable is
    /// immediate, so a clip whose active window straddled no fixed tick inside that delayed enable window would be
    /// dropped, and a continuous force/velocity would lose its unconsumed tail. When the body is no longer driven by
    /// any clip this frame, this disables the latch only if the per-activation state is already
    /// <see cref="IDrainableLatchState{TData}.IsDrained"/> (the common path — zero deactivation latency); otherwise it
    /// LINGERS the latch enabled and marks it <see cref="IOrphanedLatch.Orphaned"/> so the fixed-step apply is
    /// guaranteed at least one tick to fire/drain it. <see cref="LatchDrainFinalizeJob{TActive,TState}"/> then disables
    /// the serviced orphan. Runs single-threaded (multiple clips can bind one body) like the non-drain variant.
    /// </summary>
    [BurstCompile]
    public struct DisableAbsentDrainableTrackJob<TData, TActive, TState> : IJobChunk
        where TData : unmanaged
        where TActive : unmanaged, IComponentData, IEnableableComponent, IActive<TData>
        where TState : unmanaged, IComponentData, IDrainableLatchState<TData>
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [ReadOnly] public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly BlendData;
        [NativeDisableParallelForRestriction] public ComponentLookup<TActive> ActiveLookup;
        [NativeDisableParallelForRestriction] public ComponentLookup<TState> StateLookup;

        public JobHandle ScheduleParallel(EntityQuery query, JobHandle dependsOn)
        {
            return this.Schedule(query, dependsOn);
        }

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
                if (BlendData.ContainsKey(target)) continue; // still driven by a clip this frame
                if (!ActiveLookup.IsComponentEnabled(target)) continue; // already disabled

                // No state to consult (defensive) — behave like the plain stale-disable.
                if (!StateLookup.HasComponent(target))
                {
                    ActiveLookup.SetComponentEnabled(target, false);
                    continue;
                }

                var state = StateLookup[target];
                if (state.IsDrained(ActiveLookup[target].Config))
                {
                    // Effect already delivered this activation: disable now (zero deactivation latency, common path).
                    ActiveLookup.SetComponentEnabled(target, false);
                }
                else
                {
                    // Owes fixed-step work: keep enabled and let the fixed clock service it before disabling.
                    state.Orphaned = true;
                    StateLookup[target] = state;
                }
            }
        }
    }

    /// <summary>
    /// Fixed-step tail of the drain gate. Runs after every apply in the fixed step (see
    /// <c>PhysicsLatchDrainFinalizeSystem</c>): for each body whose latch is enabled and
    /// <see cref="IOrphanedLatch.Orphaned"/>, the apply has now serviced it this tick (fired the impulse, drained the
    /// continuous tail, or applied the missed control step), so disable the latch and clear the flag. Every disable is
    /// therefore preceded — in the same fixed tick — by an apply that observed the enabled latch, which is what
    /// guarantees no <c>Active*</c>-driven effect is dropped across the render→fixed seam.
    /// </summary>
    [BurstCompile]
    public struct LatchDrainFinalizeJob<TActive, TState> : IJobChunk
        where TActive : unmanaged, IComponentData, IEnableableComponent
        where TState : unmanaged, IComponentData, IOrphanedLatch
    {
        public ComponentTypeHandle<TActive> ActiveHandle;
        public ComponentTypeHandle<TState> StateHandle;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var states = chunk.GetNativeArray(ref StateHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var state = states[i];
                if (!state.Orphaned) continue;

                chunk.SetComponentEnabled(ref ActiveHandle, i, false);
                state.Orphaned = false;
                states[i] = state;
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

    /// <summary>
    /// Accumulates render-rate clip-active time on the bound body, per blend entry. Keyed by the per-body blend
    /// result, so overlapping clips on one body count once (unlike per-clip chunk iteration, which double-counts).
    /// </summary>
    [BurstCompile]
    public struct AdvanceElapsedTimeJob<TData, TState> : IJobParallelHashMapDefer
        where TData : unmanaged
        where TState : unmanaged, IComponentData, IElapsedTimeState
    {
        [ReadOnly] public NativeParallelHashMap<Entity, MixData<TData>>.ReadOnly BlendData;

        [NativeDisableParallelForRestriction] public ComponentLookup<TState> StateLookup;
        public float DeltaTime;

        public void ExecuteNext(int entryIndex, int jobIndex)
        {
            this.Read(BlendData, entryIndex, out var entity, out MixData<TData> _);
            if (!StateLookup.HasComponent(entity)) return;

            var s = StateLookup[entity];
            s.ElapsedTime += DeltaTime;
            StateLookup[entity] = s;
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