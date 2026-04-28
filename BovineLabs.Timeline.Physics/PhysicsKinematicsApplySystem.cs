using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.Jobs;
using BovineLabs.Reaction.Data.Core;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
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
        private ComponentTypeHandle<PhysicsForceState> _forceStateHandle;
        private ComponentTypeHandle<ActiveVelocity> _activeVelocityHandle;
        private ComponentTypeHandle<PhysicsVelocityState> _velocityStateHandle;

        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomLookup;
        private UnsafeComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyForceJob>();
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ApplyVelocityJob>();

            _forceQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, PhysicsForceState>()
                .WithAll<ActiveForce, LocalTransform>()
                .Build();

            _velocityQuery = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, PhysicsVelocityState>()
                .WithAll<ActiveVelocity, LocalTransform>()
                .Build();

            _facetHandle.Create(ref state);
            _entityHandle = state.GetEntityTypeHandle();
            
            _activeForceHandle = state.GetComponentTypeHandle<ActiveForce>(true);
            _forceStateHandle = state.GetComponentTypeHandle<PhysicsForceState>();
            
            _activeVelocityHandle = state.GetComponentTypeHandle<ActiveVelocity>(true);
            _velocityStateHandle = state.GetComponentTypeHandle<PhysicsVelocityState>();

            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomLookup = state.GetComponentLookup<TargetsCustom>(true);
            _transformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0.0001f) return;

            _facetHandle.Update(ref state);
            _entityHandle.Update(ref state);
            
            _activeForceHandle.Update(ref state);
            _forceStateHandle.Update(ref state);
            
            _activeVelocityHandle.Update(ref state);
            _velocityStateHandle.Update(ref state);

            _targetsLookup.Update(ref state);
            _targetsCustomLookup.Update(ref state);
            _transformLookup.Update(ref state);

            state.Dependency = new ApplyForceJob
            {
                DeltaTime = dt,
                FacetHandle = _facetHandle,
                EntityHandle = _entityHandle,
                ActiveForceHandle = _activeForceHandle,
                ForceStateHandle = _forceStateHandle,
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
                VelocityStateHandle = _velocityStateHandle,
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
            public ComponentTypeHandle<PhysicsForceState> ForceStateHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var forces = chunk.GetNativeArray(ref ActiveForceHandle);
                var states = chunk.GetNativeArray(ref ForceStateHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    var config = forces[i].Config;
                    var s = states[i];

                    if (config.Mode == PhysicsForceMode.Impulse && s.Fired) continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                            in TargetsCustomLookup, in TransformLookup, out var linForce);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                            in TargetsCustomLookup, in TransformLookup, out var angForce);

                    var mass = facet.Mass.IsValid
                        ? facet.Mass.ValueRO
                        : PhysicsMass.CreateKinematic(MassProperties.UnitSphere);

                    var t = config.Mode == PhysicsForceMode.Impulse ? 1f : DeltaTime;

                    PhysicsMath.ApplyLinearForce(facet.Velocity.ValueRO, mass, linForce, t,
                            out var v1);
                    PhysicsMath.ApplyAngularTorque(v1, mass, facet.Transform.ValueRO, angForce, t,
                            out var v2);
                    
                    facet.Velocity.ValueRW = v2;
                    if (config.Mode == PhysicsForceMode.Impulse)
                    {
                        s.Fired = true;
                        states[i] = s;
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
            public ComponentTypeHandle<PhysicsVelocityState> VelocityStateHandle;

            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var resolved = FacetHandle.Resolve(chunk);
                var entities = chunk.GetNativeArray(EntityHandle);
                var velocities = chunk.GetNativeArray(ref ActiveVelocityHandle);
                var states = chunk.GetNativeArray(ref VelocityStateHandle);

                for (var i = 0; i < chunk.Count; i++)
                {
                    var facet = resolved[i];
                    var config = velocities[i].Config;
                    var s = states[i];

                    var isInstant = config.Mode == PhysicsVelocityMode.SetInstant || config.Mode == PhysicsVelocityMode.AddInstant;
                    if (isInstant && s.Fired) continue;

                    PhysicsMath.ResolveSpaceVector(config.Space, config.Linear, entities[i], in TargetsLookup,
                            in TargetsCustomLookup, in TransformLookup, out var linVel);
                    PhysicsMath.ResolveSpaceVector(config.Space, config.Angular, entities[i], in TargetsLookup,
                            in TargetsCustomLookup, in TransformLookup, out var angVel);

                    var v = facet.Velocity.ValueRO;
                    
                    var isSet = config.Mode == PhysicsVelocityMode.SetContinuous || config.Mode == PhysicsVelocityMode.SetInstant;
                    if (isSet)
                    {
                        v.Linear = linVel;
                        v.Angular = angVel;
                    }
                    else
                    {
                        var t = isInstant ? 1f : DeltaTime;
                        v.Linear += linVel * t;
                        v.Angular += angVel * t;
                    }
                    
                    facet.Velocity.ValueRW = v;
                    
                    if (isInstant)
                    {
                        s.Fired = true;
                        states[i] = s;
                    }
                }
            }
        }
    }
}
