using BovineLabs.Core.ConfigVars;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.VelocityOverrides;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.VelocityClamps
{
    [Configurable]
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [UpdateAfter(typeof(PhysicsVelocityOverrideSystem))]
    [UpdateAfter(typeof(PhysicsModifierForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsVelocityClampApplySystem : ISystem
    {
        private ComponentTypeHandle<ActiveVelocityClamp> _activeHandle;
        private ComponentTypeHandle<PhysicsVelocityClampState> _stateHandle;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activeHandle = state.GetComponentTypeHandle<ActiveVelocityClamp>(true);
            _stateHandle = state.GetComponentTypeHandle<PhysicsVelocityClampState>();
            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocityClampState, PhysicsVelocity>()
                .WithAll<ActiveVelocityClamp>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _activeHandle.Update(ref state);
            _stateHandle.Update(ref state);
            _velocityHandle.Update(ref state);

            state.Dependency = new ApplyJob
            {
                ActiveHandle = _activeHandle,
                StateHandle = _stateHandle,
                VelocityHandle = _velocityHandle
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ApplyJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<ActiveVelocityClamp> ActiveHandle;
            public ComponentTypeHandle<PhysicsVelocityClampState> StateHandle;
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var states = chunk.GetNativeArray(ref StateHandle);
                var velocities = chunk.GetNativeArray(ref VelocityHandle);

                var hasActiveComponent = chunk.Has(ref ActiveHandle);
                var actives = hasActiveComponent ? chunk.GetNativeArray(ref ActiveHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var isActive = hasActiveComponent && chunk.IsComponentEnabled(ref ActiveHandle, i);
                    var state = states[i];

                    if (isActive)
                    {
                        var config = actives[i].Config;
                        var vel = velocities[i];

                        if (config.MaxLinearSpeed >= 0f)
                        {
                            var linSq = math.lengthsq(vel.Linear);
                            if (linSq > config.MaxLinearSpeed * config.MaxLinearSpeed)
                                vel.Linear = math.normalize(vel.Linear) * config.MaxLinearSpeed;
                        }

                        if (config.MaxAngularSpeed >= 0f)
                        {
                            var angSq = math.lengthsq(vel.Angular);
                            if (angSq > config.MaxAngularSpeed * config.MaxAngularSpeed)
                                vel.Angular = math.normalize(vel.Angular) * config.MaxAngularSpeed;
                        }

                        velocities[i] = vel;

                        if (!state.Fired)
                        {
                            state.Fired = true;
                            states[i] = state;
                        }
                    }
                    else if (!isActive && state.Fired)
                    {
                        state.Fired = false;
                        states[i] = state;
                    }
                }
            }
        }
    }
}