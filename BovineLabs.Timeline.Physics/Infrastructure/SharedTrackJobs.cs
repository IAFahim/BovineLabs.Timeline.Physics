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