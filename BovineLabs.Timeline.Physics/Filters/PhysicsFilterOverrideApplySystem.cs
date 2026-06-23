using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using Collider = Unity.Physics.Collider;

namespace BovineLabs.Timeline.Physics.Filters
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsFilterOverrideApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveFilterOverride> _activeHandle;
        private ComponentTypeHandle<PhysicsFilterOverrideState> _stateHandle;
        private ComponentTypeHandle<PhysicsCollider> _colliderHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveFilterOverride>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsFilterOverrideState>();
            _colliderHandle = state.GetComponentTypeHandle<PhysicsCollider>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsFilterOverrideState, PhysicsCollider>()
                .WithAll<ActiveFilterOverride>()
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
            [ReadOnly] public ComponentTypeHandle<ActiveFilterOverride> ActiveHandle;
            public ComponentTypeHandle<PhysicsFilterOverrideState> StateHandle;
            public ComponentTypeHandle<PhysicsCollider> ColliderHandle;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
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

                    if (!collider.IsUnique)
                    {
                        LogSharedColliderWarning();
                        continue;
                    }

                    var ptr = (Collider*)collider.Value.GetUnsafePtr();

                    if (isActive && !state.Fired)
                    {
                        var config = actives[i].Config;

                        var originalFilter = ptr->GetCollisionFilter();
                        state.OriginalBelongsTo = originalFilter.BelongsTo;
                        state.OriginalCollidesWith = originalFilter.CollidesWith;

                        var newFilter = originalFilter;
                        newFilter.BelongsTo = config.BelongsToOverride;
                        newFilter.CollidesWith = config.CollidesWithOverride;
                        ptr->SetCollisionFilter(newFilter);

                        state.Fired = true;
                        states[i] = state;
                    }
                    else if (isActive && state.Fired)
                    {
                        var config = actives[i].Config;
                        var currentFilter = ptr->GetCollisionFilter();
                        currentFilter.BelongsTo = config.BelongsToOverride;
                        currentFilter.CollidesWith = config.CollidesWithOverride;
                        ptr->SetCollisionFilter(currentFilter);
                    }
                    else if (!isActive && state.Fired)
                    {
                        var config = actives[i].Config;

                        if (config.RestoreOnExit)
                        {
                            var originalFilter = ptr->GetCollisionFilter();
                            originalFilter.BelongsTo = state.OriginalBelongsTo;
                            originalFilter.CollidesWith = state.OriginalCollidesWith;
                            ptr->SetCollisionFilter(originalFilter);
                        }

                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }

            [BurstDiscard]
            private static void LogSharedColliderWarning()
            {
                Debug.LogWarning(
                    "PhysicsFilterOverride targets a shared collider blob; the override was skipped. " +
                    "Enable 'Force Unique' on the bound body's collider authoring so the filter can be modified per instance.");
            }
        }
    }
}