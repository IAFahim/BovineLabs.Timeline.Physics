using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    public partial struct PhysicsDragApplySystem : ISystem
    {
        private EntityQuery _query;
        private EntityTypeHandle _entityHandle;
        private PhysicsBodyFacet.TypeHandle _facetHandle;
        private ComponentTypeHandle<ActiveDrag> _activeDragHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;
        private BufferLookup<Stat> _statLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyDragJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity>()
                .WithAll<ActiveDrag, LocalToWorld>()
                .Build();

            _entityHandle = state.GetEntityTypeHandle();
            _facetHandle.Create(ref state);
            _activeDragHandle = state.GetComponentTypeHandle<ActiveDrag>(true);

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);
            _statLookup = state.GetBufferLookup<Stat>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            _entityHandle.Update(ref state);
            _facetHandle.Update(ref state);
            _activeDragHandle.Update(ref state);

            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);
            _statLookup.Update(ref state);

            state.Dependency = new ApplyDragJob
            {
                DeltaTime = dt,
                EntityHandle = _entityHandle,
                FacetHandle = _facetHandle,
                ActiveDragHandle = _activeDragHandle,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup,
                StatLookup = _statLookup
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyDragJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveDrag> ActiveDragHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;
            [ReadOnly] public BufferLookup<Stat> StatLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityHandle);
                var resolved = FacetHandle.Resolve(chunk);
                var drags = chunk.GetNativeArray(ref ActiveDragHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var entity = entities[i];
                    var facet = resolved[i];
                    var config = drags[i].Config;

                    var targets = TargetsLookup.HasComponent(entity) ? TargetsLookup[entity] : default;
                    var multiplier = StatStrengthUtility.Resolve(in config.Strength, entity, targets, LinkSources,
                        Links, StatLookup);

                    multiplier = math.max(0f, multiplier);

                    if (multiplier <= 0.00001f) continue;

                    PhysicsMath.ComputeExponentialDecay(facet.Velocity.ValueRO, config, DeltaTime, multiplier,
                        out var vOut);
                    facet.Velocity.ValueRW = vOut;
                }
            }
        }
    }
}