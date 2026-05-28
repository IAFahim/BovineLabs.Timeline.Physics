using BovineLabs.Core.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Shared logic for draining <see cref="PendingForce"/> and <see cref="PendingVelocity"/> buffers
    /// into <see cref="PhysicsVelocity"/>. Used by both <see cref="PhysicsProducerForceAccumulatorSystem"/>
    /// and <see cref="PhysicsModifierForceAccumulatorSystem"/> at their respective drain points.
    /// </summary>
    public struct PhysicsForceAccumulator
    {
        private EntityQuery _query;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<LocalToWorld> _transformHandle;
        private ComponentTypeHandle<PhysicsMass> _massHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AccumulateJob>();

            using var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PhysicsVelocity, PendingForce>()
                .WithAll<LocalToWorld>();
            _query = state.GetEntityQuery(builder);

            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _transformHandle = state.GetComponentTypeHandle<LocalToWorld>(true);
            _massHandle = state.GetComponentTypeHandle<PhysicsMass>(true);
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
            _pendingVelocityHandle = state.GetBufferTypeHandle<PendingVelocity>();
        }

        public void OnUpdate(ref SystemState state)
        {
            _velocityHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _massHandle.Update(ref state);
            _pendingForceHandle.Update(ref state);
            _pendingVelocityHandle.Update(ref state);

            state.Dependency = new AccumulateJob
            {
                VelocityHandle = _velocityHandle,
                TransformHandle = _transformHandle,
                MassHandle = _massHandle,
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
            public BufferTypeHandle<PendingForce> PendingForceHandle;
            public BufferTypeHandle<PendingVelocity> PendingVelocityHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var velocities = chunk.GetNativeArray(ref VelocityHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);

                var hasMass = chunk.Has(ref MassHandle);
                var masses = hasMass ? chunk.GetNativeArray(ref MassHandle) : default;

                var forceAccessor = chunk.GetBufferAccessor(ref PendingForceHandle);
                var hasVelocityBuffer = chunk.Has(ref PendingVelocityHandle);
                var velocityAccessor = hasVelocityBuffer
                    ? chunk.GetBufferAccessor(ref PendingVelocityHandle)
                    : default;

                for (var i = 0; i < chunk.Count; i++)
                {
                    var velocity = velocities[i];
                    var transform = transforms[i];
                    var mass = hasMass ? masses[i] : PhysicsMass.CreateKinematic(MassProperties.UnitSphere);

                    var inverseMass = mass.InverseMass;
                    var inverseInertia = mass.InverseInertia;

                    var rotation = new quaternion(transform.Value);
                    var inverseRotation = math.inverse(rotation);

                    var forces = forceAccessor[i];
                    if (forces.Length > 0)
                    {
                        var totalLinear = float3.zero;
                        var totalAngular = float3.zero;

                        for (var f = 0; f < forces.Length; f++)
                        {
                            totalLinear += forces[f].Linear;
                            totalAngular += forces[f].Angular;
                        }

                        velocity.Linear += totalLinear * inverseMass;

                        var localAngular = math.rotate(inverseRotation, totalAngular);
                        velocity.Angular += math.rotate(rotation, localAngular * inverseInertia);

                        forces.Clear();
                    }

                    if (hasVelocityBuffer)
                    {
                        var velocityDeltas = velocityAccessor[i];
                        if (velocityDeltas.Length > 0)
                        {
                            for (var v = 0; v < velocityDeltas.Length; v++)
                            {
                                velocity.Linear += velocityDeltas[v].Linear;
                                velocity.Angular += velocityDeltas[v].Angular;
                            }

                            velocityDeltas.Clear();
                        }
                    }

                    velocities[i] = velocity;
                }
            }
        }
    }

    /// <summary>
    /// Drains <see cref="PendingForce"/> and <see cref="PendingVelocity"/> buffers at the end of
    /// <see cref="PhysicsProducerGroup"/> (before the physics step). Any system that appends to
    /// <see cref="PendingForce"/> within <see cref="PhysicsProducerGroup"/> must be ordered before
    /// this system to ensure forces are applied in the correct frame.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsKinematicsApplySystem))]
    [UpdateAfter(typeof(PhysicsPidApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsProducerForceAccumulatorSystem : ISystem
    {
        private PhysicsForceAccumulator _accumulator;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state) => _accumulator.OnCreate(ref state);
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => _accumulator.OnUpdate(ref state);
    }

    /// <summary>
    /// Drains <see cref="PendingForce"/> and <see cref="PendingVelocity"/> buffers at the end of
    /// <see cref="PhysicsModifierGroup"/> (after the physics step). Any system that appends to
    /// <see cref="PendingForce"/> within <see cref="PhysicsModifierGroup"/> must be ordered before
    /// this system to ensure forces are applied in the correct frame.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsModifierGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PhysicsModifierForceAccumulatorSystem : ISystem
    {
        private PhysicsForceAccumulator _accumulator;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state) => _accumulator.OnCreate(ref state);
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => _accumulator.OnUpdate(ref state);
    }
}