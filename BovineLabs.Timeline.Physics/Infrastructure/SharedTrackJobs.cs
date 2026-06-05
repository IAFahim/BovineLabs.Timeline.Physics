namespace BovineLabs.Timeline.Physics.Infrastructure
{

    using BovineLabs.Core.Jobs;
    using BovineLabs.Timeline.Data;
    using Data.Kernel;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;

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
                {
                    StateLookup[target] = ResetValue;
                }
            }
        }
    }

    [BurstCompile]
    public struct DisableStaleTrackJob<TActive> : IJobChunk
        where TActive : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
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
                if (ActiveLookup.HasComponent(target))
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