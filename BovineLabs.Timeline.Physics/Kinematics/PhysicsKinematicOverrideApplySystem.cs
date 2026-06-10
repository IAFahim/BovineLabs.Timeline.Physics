using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Kinematics
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsKinematicOverrideApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveKinematicOverride> _activeHandle;
        private ComponentTypeHandle<PhysicsKinematicOverrideState> _stateHandle;
        private ComponentTypeHandle<PhysicsMassOverride> _massOverrideHandle;
        private ComponentTypeHandle<PhysicsGravityFactor> _gravityFactorHandle;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<ActiveGravityOverride> _activeGravityOverrideHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveKinematicOverride>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsKinematicOverrideState>();
            _massOverrideHandle = state.GetComponentTypeHandle<PhysicsMassOverride>();
            _gravityFactorHandle = state.GetComponentTypeHandle<PhysicsGravityFactor>();
            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _activeGravityOverrideHandle = state.GetComponentTypeHandle<ActiveGravityOverride>(true);

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsKinematicOverrideState>()
                .WithAll<ActiveKinematicOverride>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _massOverrideHandle.Update(ref state);
            _gravityFactorHandle.Update(ref state);
            _velocityHandle.Update(ref state);
            _activeGravityOverrideHandle.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new ApplyJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                MassOverrideHandle = _massOverrideHandle,
                GravityFactorHandle = _gravityFactorHandle,
                VelocityHandle = _velocityHandle,
                ActiveGravityOverrideHandle = _activeGravityOverrideHandle,
                ECB = ecb
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<ActiveKinematicOverride> ActiveHandle;
            public ComponentTypeHandle<PhysicsKinematicOverrideState> StateHandle;
            public ComponentTypeHandle<PhysicsMassOverride> MassOverrideHandle;
            public ComponentTypeHandle<PhysicsGravityFactor> GravityFactorHandle;
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            [ReadOnly] public ComponentTypeHandle<ActiveGravityOverride> ActiveGravityOverrideHandle;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var states = chunk.GetNativeArray(ref StateHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var hasMassOverride = chunk.Has(ref MassOverrideHandle);
                var massOverrides = hasMassOverride ? chunk.GetNativeArray(ref MassOverrideHandle) : default;

                var hasGravityFactor = chunk.Has(ref GravityFactorHandle);
                var gravityFactors = hasGravityFactor ? chunk.GetNativeArray(ref GravityFactorHandle) : default;

                var hasVelocity = chunk.Has(ref VelocityHandle);
                var velocities = hasVelocity ? chunk.GetNativeArray(ref VelocityHandle) : default;

                var hasGravityOverride = chunk.Has(ref ActiveGravityOverrideHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var hasActiveGravityOverride = hasGravityOverride &&
                                                   chunk.IsComponentEnabled(ref ActiveGravityOverrideHandle, i);
                    var state = states[i];
                    var entity = entities[i];

                    if (isActive && !state.Fired)
                    {
                        var config = actives[i].Config;

                        if (config.ZeroVelocityOnEnter && hasVelocity)
                        {
                            var vel = velocities[i];
                            vel.Linear = float3.zero;
                            vel.Angular = float3.zero;
                            velocities[i] = vel;
                        }

                        if (hasMassOverride)
                        {
                            state.OriginalIsKinematic = massOverrides[i].IsKinematic;
                            state.AddedMassOverrideComponent = false;

                            var mo = massOverrides[i];
                            mo.IsKinematic = (byte)(config.IsKinematic ? 1 : 0);
                            massOverrides[i] = mo;
                        }
                        else
                        {
                            state.OriginalIsKinematic = 0;
                            state.AddedMassOverrideComponent = true;
                            ECB.AddComponent(unfilteredChunkIndex, entity,
                                new PhysicsMassOverride { IsKinematic = (byte)(config.IsKinematic ? 1 : 0) });
                        }

                        if (config.ZeroGravity && !hasActiveGravityOverride)
                        {
                            if (hasGravityFactor)
                            {
                                state.OriginalGravityScale = gravityFactors[i].Value;
                                state.AddedGravityComponent = false;

                                var factor = gravityFactors[i];
                                factor.Value = 0f;
                                gravityFactors[i] = factor;
                            }
                            else
                            {
                                state.OriginalGravityScale = 1f;
                                state.AddedGravityComponent = true;
                                ECB.AddComponent(unfilteredChunkIndex, entity, new PhysicsGravityFactor { Value = 0f });
                            }
                        }

                        state.Fired = true;
                        states[i] = state;
                    }
                    else if (isActive && state.Fired)
                    {
                        var config = actives[i].Config;

                        if (hasMassOverride)
                        {
                            var mo = massOverrides[i];
                            mo.IsKinematic = (byte)(config.IsKinematic ? 1 : 0);
                            massOverrides[i] = mo;
                        }

                        if (config.ZeroGravity && hasGravityFactor && !hasActiveGravityOverride)
                        {
                            var factor = gravityFactors[i];
                            factor.Value = 0f;
                            gravityFactors[i] = factor;
                        }
                    }
                    else if (!isActive && state.Fired)
                    {
                        if (state.AddedMassOverrideComponent)
                        {
                            ECB.RemoveComponent<PhysicsMassOverride>(unfilteredChunkIndex, entity);
                        }
                        else if (hasMassOverride)
                        {
                            var mo = massOverrides[i];
                            mo.IsKinematic = state.OriginalIsKinematic;
                            massOverrides[i] = mo;
                        }
                        else
                        {
                            ECB.AddComponent(unfilteredChunkIndex, entity,
                                new PhysicsMassOverride { IsKinematic = state.OriginalIsKinematic });
                        }

                        if (!hasActiveGravityOverride)
                        {
                            if (state.AddedGravityComponent)
                            {
                                ECB.RemoveComponent<PhysicsGravityFactor>(unfilteredChunkIndex, entity);
                            }
                            else if (hasGravityFactor)
                            {
                                var factor = gravityFactors[i];
                                factor.Value = state.OriginalGravityScale;
                                gravityFactors[i] = factor;
                            }
                            else
                            {
                                ECB.AddComponent(unfilteredChunkIndex, entity,
                                    new PhysicsGravityFactor { Value = state.OriginalGravityScale });
                            }
                        }

                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }
        }
    }
}
