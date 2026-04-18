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
    public partial struct PhysicsPidApplySystem : ISystem
    {
        private EntityQuery _linearQuery;
        private EntityQuery _angularQuery;

        private PhysicsBodyFacet.TypeHandle facetHandle;
        private EntityTypeHandle entityHandle;
        
        private ComponentTypeHandle<PhysicsLinearPIDState> linearStateHandle;
        private ComponentTypeHandle<ActiveLinearPid> activeLinearHandle;

        private ComponentTypeHandle<PhysicsAngularPIDState> angularStateHandle;
        private ComponentTypeHandle<ActiveAngularPid> activeAngularHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyLinearJob>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyAngularJob>();

            _linearQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Unity.Physics.PhysicsVelocity, PhysicsLinearPIDState>()
                .WithAll<ActiveLinearPid, LocalTransform>()
                .Build();

            _angularQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Unity.Physics.PhysicsVelocity, PhysicsAngularPIDState>()
                .WithAll<ActiveAngularPid, LocalTransform>()
                .Build();

            facetHandle.Create(ref state);
            entityHandle = state.GetEntityTypeHandle();

            linearStateHandle = state.GetComponentTypeHandle<PhysicsLinearPIDState>();
            activeLinearHandle = state.GetComponentTypeHandle<ActiveLinearPid>(true);

            angularStateHandle = state.GetComponentTypeHandle<PhysicsAngularPIDState>();
            activeAngularHandle = state.GetComponentTypeHandle<ActiveAngularPid>(true);

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            facetHandle.Update(ref state);
            entityHandle.Update(ref state);
            
            linearStateHandle.Update(ref state);
            activeLinearHandle.Update(ref state);
            
            angularStateHandle.Update(ref state);
            activeAngularHandle.Update(ref state);

            _targetsLookup.Update(ref state);
            _targetsCustomLookup.Update(ref state);
            _transformLookup.Update(ref state);

            state.Dependency = new ApplyLinearJob
            {
                DeltaTime = dt,
                FacetHandle = facetHandle,
                EntityHandle = entityHandle,
                StateHandle = linearStateHandle,
                ActiveHandle = activeLinearHandle,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
                TransformLookup = _transformLookup
            }.ScheduleParallel(_linearQuery, state.Dependency);

            state.Dependency = new ApplyAngularJob
            {
                DeltaTime = dt,
                FacetHandle = facetHandle,
                EntityHandle = entityHandle,
                StateHandle = angularStateHandle,
                ActiveHandle = activeAngularHandle,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomLookup,
                TransformLookup = _transformLookup
            }.ScheduleParallel(_angularQuery, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyLinearJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            public ComponentTypeHandle<PhysicsLinearPIDState> StateHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveLinearPid> ActiveHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var states = chunk.GetNativeArray(ref StateHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    
                    if (!PhysicsMath.TryResolveLinearPidTarget(facet.Transform.ValueRO, actives[i].Config, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var targetPos))
                    {
                        continue;
                    }

                    var error = facet.Transform.ValueRO.Position - targetPos;
                    if (!PhysicsMath.TryComputePidForce(error, actives[i].Config.Tuning, states[i].State, DeltaTime, out var force, out var nextState))
                    {
                        continue;
                    }

                    var mass = facet.Mass.IsValid ? facet.Mass.ValueRO : Unity.Physics.PhysicsMass.CreateKinematic(Unity.Physics.MassProperties.UnitSphere);

                    if (PhysicsMath.TryApplyLinearForce(facet.Velocity.ValueRO, mass, -force, DeltaTime, out var nextVelocity))
                    {
                        facet.Velocity.ValueRW = nextVelocity;
                        
                        var s = states[i];
                        s.State = nextState;
                        states[i] = s;
                    }
                }
            }
        }

        [BurstCompile]
        private struct ApplyAngularJob : IJobChunkWorkerBeginEnd
        {
            public float DeltaTime;
            public PhysicsBodyFacet.TypeHandle FacetHandle;
            [ReadOnly] public EntityTypeHandle EntityHandle;
            public ComponentTypeHandle<PhysicsAngularPIDState> StateHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveAngularPid> ActiveHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var states = chunk.GetNativeArray(ref StateHandle);
                var actives = chunk.GetNativeArray(ref ActiveHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];

                    if (!PhysicsMath.TryResolveAngularPidTarget(facet.Transform.ValueRO, actives[i].Config, entities[i], in TargetsLookup, in TargetsCustomLookup, in TransformLookup, out var targetRot) ||
                        !PhysicsMath.TryComputeAngularError(facet.Transform.ValueRO.Rotation, targetRot, out var error) ||
                        !PhysicsMath.TryComputePidForce(error, actives[i].Config.Tuning, states[i].State, DeltaTime, out var torque, out var nextState))
                    {
                        continue;
                    }

                    var mass = facet.Mass.IsValid ? facet.Mass.ValueRO : Unity.Physics.PhysicsMass.CreateKinematic(Unity.Physics.MassProperties.UnitSphere);

                    if (PhysicsMath.TryApplyAngularTorque(facet.Velocity.ValueRO, mass, facet.Transform.ValueRO, torque, DeltaTime, out var nextVelocity))
                    {
                        facet.Velocity.ValueRW = nextVelocity;
                        
                        var s = states[i];
                        s.State = nextState;
                        states[i] = s;
                    }
                }
            }
        }
    }
}