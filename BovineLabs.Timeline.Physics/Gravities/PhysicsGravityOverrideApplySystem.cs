using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Gravities
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsGravityOverrideApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveGravityOverride> _activeHandle;
        private ComponentTypeHandle<PhysicsGravityOverrideState> _stateHandle;
        private ComponentTypeHandle<PhysicsGravityFactor> _gravityFactorHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveGravityOverride>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsGravityOverrideState>();
            _gravityFactorHandle = state.GetComponentTypeHandle<PhysicsGravityFactor>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsGravityOverrideState>()
                .WithAll<ActiveGravityOverride>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _gravityFactorHandle.Update(ref state);

            var ecbSystem = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            state.Dependency = new ApplyJob
            {
                EntityType = SystemAPI.GetEntityTypeHandle(),
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                GravityFactorHandle = _gravityFactorHandle,
                ECB = ecb
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityType;
            [ReadOnly] public ComponentTypeHandle<ActiveGravityOverride> ActiveHandle;
            public ComponentTypeHandle<PhysicsGravityOverrideState> StateHandle;
            public ComponentTypeHandle<PhysicsGravityFactor> GravityFactorHandle;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(EntityType);
                var states = chunk.GetNativeArray(ref StateHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var hasGravityFactor = chunk.Has(ref GravityFactorHandle);
                var gravityFactors = hasGravityFactor ? chunk.GetNativeArray(ref GravityFactorHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];
                    var entity = entities[i];

                    if (isActive && !state.Fired)
                    {
                        var config = actives[i].Config;

                        if (hasGravityFactor)
                        {
                            state.OriginalGravityScale = gravityFactors[i].Value;
                            state.AddedComponent = false;

                            var factor = gravityFactors[i];
                            factor.Value = config.GravityScale;
                            gravityFactors[i] = factor;
                        }
                        else
                        {
                            state.OriginalGravityScale = 1f;
                            state.AddedComponent = true;
                            ECB.AddComponent(unfilteredChunkIndex, entity,
                                new PhysicsGravityFactor { Value = config.GravityScale });
                        }

                        state.Fired = true;
                        states[i] = state;
                    }
                    else if (isActive && state.Fired)
                    {
                        var config = actives[i].Config;

                        if (hasGravityFactor)
                        {
                            var factor = gravityFactors[i];
                            factor.Value = config.GravityScale;
                            gravityFactors[i] = factor;
                        }
                        else
                        {
                            ECB.AddComponent(unfilteredChunkIndex, entity,
                                new PhysicsGravityFactor { Value = config.GravityScale });
                        }
                    }
                    else if (!isActive && state.Fired)
                    {
                        var config = actives[i].Config;

                        if (config.RestoreOnExit)
                        {
                            if (state.AddedComponent)
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