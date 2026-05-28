using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    [BurstCompile]
    public struct ResetStateTrackJob<TState> : IJobChunk
        where TState : unmanaged, IComponentData
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [NativeDisableParallelForRestriction] public ComponentLookup<TState> StateLookup;
        public TState ResetValue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target != Entity.Null && StateLookup.HasComponent(target))
                    StateLookup[target] = ResetValue;
            }
        }
    }

    [BurstCompile]
    public struct DisableStaleTrackJob<TActive> : IJobChunk
        where TActive : unmanaged, IComponentData, IEnableableComponent
    {
        [ReadOnly] public ComponentTypeHandle<TrackBinding> TrackBindingTypeHandle;
        [ReadOnly] public ComponentLookup<TActive> ActiveLookup;
        public EntityCommandBuffer.ParallelWriter ECB;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target == Entity.Null) continue;
                if (ActiveLookup.HasComponent(target))
                    ECB.SetComponentEnabled<TActive>(unfilteredChunkIndex, target, false);
            }
        }
    }
}
