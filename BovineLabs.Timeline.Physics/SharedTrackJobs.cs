using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
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

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var bindings = chunk.GetNativeArray(ref TrackBindingTypeHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out var i))
            {
                var target = bindings[i].Value;
                if (target == Entity.Null || !StateLookup.HasComponent(target)) continue;

                // Only reset the state if there isn't already an active clip running on this target.
                // If TActive is enabled, another track is currently driving this target entity.
                // This prevents overlapping clips from wiping out each other's state (e.g. PID integral).
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

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
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
}
