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
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsKinematicsApplySystem))]
    [UpdateAfter(typeof(PhysicsPidApplySystem))]
    public partial struct PhysicsForceAccumulatorSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<LocalTransform> _transformHandle;
        private ComponentTypeHandle<PhysicsMass> _massHandle;
        private BufferTypeHandle<PendingForce> _pendingForceHandle;
        private BufferTypeHandle<PendingVelocity> _pendingVelocityHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<AccumulateJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, PendingForce>()
                .WithAll<LocalTransform>()
                .Build();

            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _transformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _massHandle = state.GetComponentTypeHandle<PhysicsMass>(true);
            _pendingForceHandle = state.GetBufferTypeHandle<PendingForce>();
            _pendingVelocityHandle = state.GetBufferTypeHandle<PendingVelocity>();
        }

        [BurstCompile]
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
            [ReadOnly] public ComponentTypeHandle<LocalTransform> TransformHandle;
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

                    var inverseMass = mass.InverseMass > 0f ? mass.InverseMass : 1f;
                    var inverseInertia = math.any(mass.InverseInertia > 0f) ? mass.InverseInertia : new float3(1f);
                    var inverseRotation = math.inverse(transform.Rotation);

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
                        velocity.Angular += math.rotate(transform.Rotation, localAngular * inverseInertia);

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
}