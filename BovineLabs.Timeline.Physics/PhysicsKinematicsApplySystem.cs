using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Jobs;
using BovineLabs.Reaction.Data.Core;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    [Configurable]
    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    public partial struct PhysicsKinematicsApplySystem : ISystem
    {
        private EntityQuery _forceQuery;
        private EntityQuery _velocityQuery;

        private PhysicsBodyFacet.TypeHandle _facetHandle;
        private EntityTypeHandle _entityHandle;
        private ComponentTypeHandle<ActiveForce> _activeForceHandle;
        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyForceJob>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyVelocityJob>();

            _forceQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Unity.Physics.PhysicsVelocity>()
                .WithAll<ActiveForce, LocalTransform>()
                .Build();

            _velocityQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Unity.Physics.PhysicsVelocity>()
                .WithAll<ActiveVelocity, LocalTransform>()
                .Build();

            _facetHandle.Create(ref state);
            _entityHandle = state.GetEntityTypeHandle();
            _activeForceHandle = state.GetComponentTypeHandle<ActiveForce>(true);
            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            _facetHandle.Update(ref state);
            _entityHandle.Update(ref state);
            _activeForceHandle.Update(ref state);
            _activeVelocityHandle.Update(ref state);

            _targetsLookup.Update(ref state);
            _targetsCustomLookup.Update(ref state);
            _transformLookup.Update(ref state);

            state.Dependency = new ApplyForceJob
            {
                DeltaTime = dt,
                FacetHandle = _facetHandle,
                EntityHandle = _entityHandle,
                ActiveForceHandle = _activeForceHandle,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
                TransformLookup = _transformLookup
            }.ScheduleParallel(_forceQuery, state.Dependency);

            state.Dependency = new ApplyVelocityJob
            {
                DeltaTime = dt,
                FacetHandle = _facetHandle,
                EntityHandle = _entityHandle,
                ActiveVelocityHandle = _activeVelocityHandle,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
                TransformLookup = _transformLookup
            }.ScheduleParallel(_velocityQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyForceJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveForce> ActiveForceHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var forces = chunk.GetNativeArray(ref ActiveForceHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    var config = forces[i].Config;

                    if (!PhysicsMath.TryResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var linForce) ||
                        !PhysicsMath.TryResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var angForce))
                    {
                        continue;
                    }

                    var mass = facet.Mass.IsValid ? facet.Mass.ValueRO : Unity.Physics.PhysicsMass.CreateKinematic(Unity.Physics.MassProperties.UnitSphere);

                    if (PhysicsMath.TryApplyLinearForce(facet.Velocity.ValueRO, mass, linForce, DeltaTime, out var v1) &&
                        PhysicsMath.TryApplyAngularTorque(v1, mass, facet.Transform.ValueRO, angForce, DeltaTime, out var v2))
                    {
                        facet.Velocity.ValueRW = v2;
                    }
                }
            }
        }

        [BurstCompile]
        private struct ApplyVelocityJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveVelocity> ActiveVelocityHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var velocities = chunk.GetNativeArray(ref ActiveVelocityHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    var config = velocities[i].Config;

                    if (PhysicsMath.TryResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var linVel) &&
                        PhysicsMath.TryResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var angVel))
                    {
                        var v = facet.Velocity.ValueRO;
                        v.Linear += linVel * DeltaTime;
                        v.Angular += angVel * DeltaTime;
                        facet.Velocity.ValueRW = v;
                    }
                }
            }
        }
    }
}