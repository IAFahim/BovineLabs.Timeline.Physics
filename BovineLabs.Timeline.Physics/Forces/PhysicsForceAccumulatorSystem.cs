using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Kinematics;
using BovineLabs.Timeline.Physics.PIDs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Forces
{
    public struct PhysicsForceAccumulator
    {
        private EntityQuery _query;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<LocalToWorld> _transformHandle;
        private ComponentTypeHandle<PhysicsMass> _massHandle;
        private ComponentTypeHandle<PendingVelocityReset> _resetHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AccumulateJob>();

            using var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PhysicsVelocity>()
                .WithAll<LocalToWorld>()
                .WithAnyRW<PendingForce, PendingVelocity>();
            _query = state.GetEntityQuery(builder);

            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _transformHandle = state.GetComponentTypeHandle<LocalToWorld>(true);
            _massHandle = state.GetComponentTypeHandle<PhysicsMass>(true);
            _resetHandle = state.GetComponentTypeHandle<PendingVelocityReset>();
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
            _pendingVelocityHandle = state.GetBufferTypeHandle<PendingVelocity>();

            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            _velocityHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _massHandle.Update(ref state);
            _resetHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);
            _pendingVelocityHandle.Update(ref state);

            state.Dependency = new AccumulateJob
            {
                VelocityHandle = _velocityHandle,
                TransformHandle = _transformHandle,
                MassHandle = _massHandle,
                ResetHandle = _resetHandle,
                PendingForceHandle = _pendingForceHandle,
                PendingVelocityHandle = _pendingVelocityHandle
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct AccumulateJob : IJobChunkWorkerBeginEnd
        {
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsMass> MassHandle;
            public ComponentTypeHandle<PendingVelocityReset> ResetHandle;
            public BufferTypeHandle<PendingForce> PendingForceHandle;
            public BufferTypeHandle<PendingVelocity> PendingVelocityHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var velocities = chunk.GetNativeArray(ref VelocityHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);

                var hasMass = chunk.Has(ref MassHandle);
                var masses = hasMass ? chunk.GetNativeArray(ref MassHandle) : default;

                var hasReset = chunk.Has(ref ResetHandle);
                var resets = hasReset ? chunk.GetNativeArray(ref ResetHandle) : default;

                var hasForceBuffer = chunk.Has(ref PendingForceHandle);
                var forceAccessor = hasForceBuffer ? chunk.GetBufferAccessor(ref PendingForceHandle) : default;

                var hasVelocityBuffer = chunk.Has(ref PendingVelocityHandle);
                var velocityAccessor = hasVelocityBuffer
                    ? chunk.GetBufferAccessor(ref PendingVelocityHandle)
                    : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var velocity = velocities[i];
                    var dirty = false;

                    if (hasReset && chunk.IsComponentEnabled(ref ResetHandle, i))
                    {
                        ApplyReset(ref velocity, resets[i].Flags);
                        resets[i] = default;
                        chunk.SetComponentEnabled(ref ResetHandle, i, false);
                        dirty = true;
                    }

                    if (hasForceBuffer)
                    {
                        var mass = hasMass ? masses[i] : PhysicsMass.CreateKinematic(MassProperties.UnitSphere);
                        dirty |= AccumulateForces(ref velocity, forceAccessor[i], mass, transforms[i]);
                    }

                    if (hasVelocityBuffer)
                    {
                        dirty |= AccumulateVelocities(ref velocity, velocityAccessor[i]);
                    }

                    if (dirty)
                    {
                        velocities[i] = velocity;
                    }
                }
            }

            private static void ApplyReset(ref PhysicsVelocity velocity, VelocityResetFlags flags)
            {
                velocity.Linear = math.select(velocity.Linear, float3.zero, (flags & VelocityResetFlags.Linear) != 0);
                velocity.Angular = math.select(velocity.Angular, float3.zero, (flags & VelocityResetFlags.Angular) != 0);
            }

            private static bool AccumulateForces(ref PhysicsVelocity velocity, DynamicBuffer<PendingForce> forces,
                in PhysicsMass mass, in LocalToWorld transform)
            {
                if (forces.Length == 0)
                {
                    return false;
                }

                var totalLinear = float3.zero;
                var totalAngular = float3.zero;

                for (var f = 0; f < forces.Length; f++)
                {
                    totalLinear += forces[f].Linear;
                    totalAngular += forces[f].Angular;
                }

                velocity.Linear += totalLinear * mass.InverseMass;

                var rotation = new quaternion(math.orthonormalize(new float3x3(transform.Value)));
                var inertiaRot = math.mul(rotation, mass.Transform.rot);
                var localAngular = math.rotate(math.inverse(inertiaRot), totalAngular);
                velocity.Angular += math.rotate(inertiaRot, localAngular * mass.InverseInertia);

                forces.Clear();
                return true;
            }

            private static bool AccumulateVelocities(ref PhysicsVelocity velocity, DynamicBuffer<PendingVelocity> deltas)
            {
                if (deltas.Length == 0)
                {
                    return false;
                }

                for (var v = 0; v < deltas.Length; v++)
                {
                    velocity.Linear += deltas[v].Linear;
                    velocity.Angular += deltas[v].Angular;
                }

                deltas.Clear();
                return true;
            }
        }
    }

    /// <summary>
    ///     Drains <see cref="PendingVelocityReset" /> requests, then <see cref="PendingForce" /> and
    ///     <see cref="PendingVelocity" /> buffers, at the end of <see cref="PhysicsProducerGroup" />
    ///     (before the physics step). Any system that appends to these within
    ///     <see cref="PhysicsProducerGroup" /> must be ordered before this system to ensure forces are
    ///     applied in the correct frame.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsKinematicsApplySystem))]
    [UpdateAfter(typeof(PhysicsPidApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsProducerForceAccumulatorSystem : ISystem
    {
        private PhysicsForceAccumulator _accumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _accumulator.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _accumulator.OnUpdate(ref state);
        }
    }

    /// <summary>
    ///     Drains <see cref="PendingVelocityReset" /> requests, then <see cref="PendingForce" /> and
    ///     <see cref="PendingVelocity" /> buffers, at the end of <see cref="PhysicsModifierGroup" />
    ///     (after the physics step). Any system that appends to these within
    ///     <see cref="PhysicsModifierGroup" /> must be ordered before this system to ensure forces are
    ///     applied in the correct frame.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsModifierForceAccumulatorSystem : ISystem
    {
        private PhysicsForceAccumulator _accumulator;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _accumulator.OnCreate(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _accumulator.OnUpdate(ref state);
        }
    }
}
