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