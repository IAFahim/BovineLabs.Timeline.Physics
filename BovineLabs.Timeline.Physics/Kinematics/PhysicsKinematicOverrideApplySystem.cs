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
    [BurstCompile]
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

                var lanes = new Lanes
                {
                    HasMassOverride = chunk.Has(ref MassOverrideHandle),
                    HasGravityFactor = chunk.Has(ref GravityFactorHandle),
                    HasVelocity = chunk.Has(ref VelocityHandle)
                };
                lanes.MassOverrides = lanes.HasMassOverride ? chunk.GetNativeArray(ref MassOverrideHandle) : default;
                lanes.GravityFactors = lanes.HasGravityFactor ? chunk.GetNativeArray(ref GravityFactorHandle) : default;
                lanes.Velocities = lanes.HasVelocity ? chunk.GetNativeArray(ref VelocityHandle) : default;

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
                        OnEnter(ref lanes, ref state, actives[i].Config, hasActiveGravityOverride, entity,
                            unfilteredChunkIndex, i);
                        states[i] = state;
                    }
                    else if (isActive && state.Fired)
                    {
                        OnStay(ref lanes, ref state, actives[i].Config, hasActiveGravityOverride, entity,
                            unfilteredChunkIndex, i);
                        states[i] = state;
                    }
                    else if (!isActive && state.Fired)
                    {
                        OnExit(ref lanes, ref state, hasActiveGravityOverride, entity, unfilteredChunkIndex, i);
                        states[i] = state;
                    }
                }
            }

            private void OnEnter(ref Lanes lanes, ref PhysicsKinematicOverrideState state,
                in PhysicsKinematicOverrideData config, bool hasActiveGravityOverride, Entity entity, int chunkIndex,
                int i)
            {
                if (config.ZeroVelocityOnEnter && lanes.HasVelocity)
                {
                    var vel = lanes.Velocities[i];
                    vel.Linear = float3.zero;
                    vel.Angular = float3.zero;
                    lanes.Velocities[i] = vel;
                }

                if (lanes.HasMassOverride)
                {
                    state.OriginalIsKinematic = lanes.MassOverrides[i].IsKinematic;
                    state.AddedMassOverrideComponent = false;

                    var mo = lanes.MassOverrides[i];
                    mo.IsKinematic = (byte)(config.IsKinematic ? 1 : 0);
                    lanes.MassOverrides[i] = mo;
                }
                else
                {
                    state.OriginalIsKinematic = 0;
                    state.AddedMassOverrideComponent = true;
                    ECB.AddComponent(chunkIndex, entity,
                        new PhysicsMassOverride { IsKinematic = (byte)(config.IsKinematic ? 1 : 0) });
                }

                if (config.ZeroGravity && !hasActiveGravityOverride)
                {
                    state.GravityCaptured = true;

                    if (lanes.HasGravityFactor)
                    {
                        state.OriginalGravityScale = lanes.GravityFactors[i].Value;
                        state.AddedGravityComponent = false;

                        var factor = lanes.GravityFactors[i];
                        factor.Value = 0f;
                        lanes.GravityFactors[i] = factor;
                    }
                    else
                    {
                        state.OriginalGravityScale = 1f;
                        state.AddedGravityComponent = true;
                        ECB.AddComponent(chunkIndex, entity, new PhysicsGravityFactor { Value = 0f });
                    }
                }
                else
                {
                    state.GravityCaptured = false;
                }

                state.Fired = true;
            }

            private void OnStay(ref Lanes lanes, ref PhysicsKinematicOverrideState state,
                in PhysicsKinematicOverrideData config, bool hasActiveGravityOverride, Entity entity, int chunkIndex,
                int i)
            {
                if (lanes.HasMassOverride)
                {
                    var mo = lanes.MassOverrides[i];
                    mo.IsKinematic = (byte)(config.IsKinematic ? 1 : 0);
                    lanes.MassOverrides[i] = mo;
                }

                if (!config.ZeroGravity || hasActiveGravityOverride) return;

                if (!state.GravityCaptured)
                {
                    state.GravityCaptured = true;

                    if (lanes.HasGravityFactor)
                    {
                        state.OriginalGravityScale = lanes.GravityFactors[i].Value;
                        state.AddedGravityComponent = false;
                    }
                    else
                    {
                        state.OriginalGravityScale = 1f;
                        state.AddedGravityComponent = true;
                        ECB.AddComponent(chunkIndex, entity, new PhysicsGravityFactor { Value = 0f });
                    }
                }

                if (lanes.HasGravityFactor)
                {
                    var factor = lanes.GravityFactors[i];
                    factor.Value = 0f;
                    lanes.GravityFactors[i] = factor;
                }
            }

            private void OnExit(ref Lanes lanes, ref PhysicsKinematicOverrideState state, bool hasActiveGravityOverride,
                Entity entity, int chunkIndex, int i)
            {
                if (state.AddedMassOverrideComponent)
                {
                    ECB.RemoveComponent<PhysicsMassOverride>(chunkIndex, entity);
                }
                else if (lanes.HasMassOverride)
                {
                    var mo = lanes.MassOverrides[i];
                    mo.IsKinematic = state.OriginalIsKinematic;
                    lanes.MassOverrides[i] = mo;
                }
                else
                {
                    ECB.AddComponent(chunkIndex, entity,
                        new PhysicsMassOverride { IsKinematic = state.OriginalIsKinematic });
                }

                if (state.GravityCaptured && !hasActiveGravityOverride)
                {
                    if (state.AddedGravityComponent)
                    {
                        ECB.RemoveComponent<PhysicsGravityFactor>(chunkIndex, entity);
                    }
                    else if (lanes.HasGravityFactor)
                    {
                        var factor = lanes.GravityFactors[i];
                        factor.Value = state.OriginalGravityScale;
                        lanes.GravityFactors[i] = factor;
                    }
                    else
                    {
                        ECB.AddComponent(chunkIndex, entity,
                            new PhysicsGravityFactor { Value = state.OriginalGravityScale });
                    }
                }

                state.Fired = false;
            }

            private struct Lanes
            {
                public NativeArray<PhysicsMassOverride> MassOverrides;
                public NativeArray<PhysicsGravityFactor> GravityFactors;
                public NativeArray<PhysicsVelocity> Velocities;
                public bool HasMassOverride;
                public bool HasGravityFactor;
                public bool HasVelocity;
            }
        }
    }
}