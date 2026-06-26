using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Shapes
{
    // Mirror of PhysicsShapeResizeApplySystem but SWAPS the whole collider blob reference (box -> sphere etc.)
    // instead of mutating geometry. Re-pointing PhysicsCollider.Value is share-safe (no Force Unique needed) and the
    // baked replacement blob is owned by the subscene, so nothing is allocated/disposed at runtime. Captures the
    // original blob on enter and restores it on exit.
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsShapeSwapApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveShapeSwap> _activeHandle;
        private ComponentTypeHandle<PhysicsShapeSwapState> _stateHandle;
        private ComponentTypeHandle<PhysicsCollider> _colliderHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveShapeSwap>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsShapeSwapState>();
            _colliderHandle = state.GetComponentTypeHandle<PhysicsCollider>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsShapeSwapState, PhysicsCollider>()
                .WithAll<ActiveShapeSwap>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _colliderHandle.Update(ref state);

            state.Dependency = new ApplyJob
            {
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                ColliderHandle = _colliderHandle
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<ActiveShapeSwap> ActiveHandle;
            public ComponentTypeHandle<PhysicsShapeSwapState> StateHandle;
            public ComponentTypeHandle<PhysicsCollider> ColliderHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref StateHandle);
                var colliders = chunk.GetNativeArray(ref ColliderHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];
                    var collider = colliders[i];
                    if (!collider.IsValid) continue;

                    if (isActive && !state.Fired)
                    {
                        var config = actives[i].Config;
                        if (!config.NewCollider.IsCreated) continue;

                        state.Original = collider.Value;
                        collider.Value = config.NewCollider;
                        colliders[i] = collider;

                        state.Fired = true;
                        states[i] = state;
                    }
                    else if (isActive && state.Fired)
                    {
                        var config = actives[i].Config;
                        // Idempotent + crossfade-safe; only write (bumping the physics-rebuild version) when changed.
                        if (config.NewCollider.IsCreated && !collider.Value.Equals(config.NewCollider))
                        {
                            collider.Value = config.NewCollider;
                            colliders[i] = collider;
                        }
                    }
                    else if (!isActive && state.Fired)
                    {
                        var config = actives[i].Config;
                        if (config.RestoreOnExit && state.Original.IsCreated &&
                            !collider.Value.Equals(state.Original))
                        {
                            collider.Value = state.Original;
                            colliders[i] = collider;
                        }

                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }
        }
    }
}
