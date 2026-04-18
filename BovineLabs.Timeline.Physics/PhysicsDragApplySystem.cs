using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsDragApplySystem : ISystem
    {
        private EntityQuery _query;
        private PhysicsBodyFacet.TypeHandle _facetHandle;
        private ComponentTypeHandle<ActiveDrag> _activeDragHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyDragJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<Unity.Physics.PhysicsVelocity>()
                .WithAll<ActiveDrag, Unity.Transforms.LocalTransform>()
                .Build();

            _facetHandle.Create(ref state);
            _activeDragHandle = state.GetComponentTypeHandle<ActiveDrag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f)
            {
                return;
            }

            _facetHandle.Update(ref state);
            _activeDragHandle.Update(ref state);

            state.Dependency = new ApplyDragJob
            {
                DeltaTime = dt,
                FacetHandle = _facetHandle,
                ActiveDragHandle = _activeDragHandle
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyDragJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveDrag> ActiveDragHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var drags = chunk.GetNativeArray(ref ActiveDragHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    if (PhysicsMath.TryComputeExponentialDecay(facet.Velocity.ValueRO, drags[i].Config, DeltaTime, out var vOut))
                    {
                        facet.Velocity.ValueRW = vOut;
                    }
                }
            }
        }
    }
}