using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Jobs;
using BovineLabs.Timeline.Physics.Data.Forces;
using BovineLabs.Timeline.Physics.Drags;
using BovineLabs.Timeline.Physics.Infrastructure;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Forces
{
    [Configurable]
    public static class ExternalVelocityConfig
    {
        [ConfigVar("external-velocity.decay-rate", 8f,
            "Per-second exponential decay rate of the external (knockback) velocity channel. Higher = the hit fades " +
            "faster (more impulse-like); lower = the body slides further before recovering. ~8 ≈ a 0.15s slide.")]
        public static readonly SharedStatic<float> DecayRate = SharedStatic<float>.GetOrCreate<DecayRateTag>();

        private struct DecayRateTag
        {
        }
    }

    /// <summary>
    /// Producer seam (runs just before the physics solver). Drains the <see cref="PendingExternalForce"/> inbox into
    /// the standing <see cref="ExternalVelocity"/> channel (mass-converted exactly like intent forces), then adds that
    /// channel on top of <see cref="PhysicsVelocity"/> so the solver — collisions, friction, restitution — responds to
    /// the hit. The matching <see cref="PhysicsExternalVelocityDecomposeSystem"/> removes it again after the solver.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateAfter(typeof(PhysicsProducerForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsExternalVelocityComposeSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<ExternalVelocity> _externalHandle;
        private ComponentTypeHandle<LocalToWorld> _transformHandle;
        private ComponentTypeHandle<PhysicsMass> _massHandle;
        private BufferTypeHandle<PendingExternalForce> _inboxHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<ComposeJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, ExternalVelocity>()
                .WithAll<LocalToWorld>()
                .Build();

            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _externalHandle = state.GetComponentTypeHandle<ExternalVelocity>();
            _transformHandle = state.GetComponentTypeHandle<LocalToWorld>(true);
            _massHandle = state.GetComponentTypeHandle<PhysicsMass>(true);
            _inboxHandle = state.GetBufferTypeHandle<PendingExternalForce>();

            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _velocityHandle.Update(ref state);
            _externalHandle.Update(ref state);
            _transformHandle.Update(ref state);
            _massHandle.Update(ref state);
            _inboxHandle.Update(ref state);

            state.Dependency = new ComposeJob
            {
                VelocityHandle = _velocityHandle,
                ExternalHandle = _externalHandle,
                TransformHandle = _transformHandle,
                MassHandle = _massHandle,
                InboxHandle = _inboxHandle,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct ComposeJob : IJobChunkWorkerBeginEnd
        {
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            public ComponentTypeHandle<ExternalVelocity> ExternalHandle;
            [ReadOnly] public ComponentTypeHandle<LocalToWorld> TransformHandle;
            [ReadOnly] public ComponentTypeHandle<PhysicsMass> MassHandle;
            public BufferTypeHandle<PendingExternalForce> InboxHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var velocities = chunk.GetNativeArray(ref VelocityHandle);
                var externals = chunk.GetNativeArray(ref ExternalHandle);
                var transforms = chunk.GetNativeArray(ref TransformHandle);

                var hasMass = chunk.Has(ref MassHandle);
                var masses = hasMass ? chunk.GetNativeArray(ref MassHandle) : default;

                var hasInbox = chunk.Has(ref InboxHandle);
                var inboxAccessor = hasInbox ? chunk.GetBufferAccessor(ref InboxHandle) : default;

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var external = externals[i];

                    if (hasInbox)
                    {
                        var inbox = inboxAccessor[i];
                        if (inbox.Length > 0)
                        {
                            var totalLinear = float3.zero;
                            var totalAngular = float3.zero;
                            for (var f = 0; f < inbox.Length; f++)
                            {
                                totalLinear += inbox[f].Linear;
                                totalAngular += inbox[f].Angular;
                            }

                            inbox.Clear();

                            var mass = hasMass
                                ? masses[i]
                                : PhysicsMass.CreateKinematic(MassProperties.UnitSphere);
                            var delta = ForceInertiaKernel.ApplyForcesToVelocity(default(PhysicsVelocity), totalLinear,
                                totalAngular, mass, transforms[i]);
                            external.Linear += delta.Linear;
                            external.Angular += delta.Angular;
                            externals[i] = external;
                        }
                    }

                    if (math.lengthsq(external.Linear) > 0f || math.lengthsq(external.Angular) > 0f)
                    {
                        var velocity = velocities[i];
                        velocity.Linear += external.Linear;
                        velocity.Angular += external.Angular;
                        velocities[i] = velocity;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Modifier seam (runs first, right after the solver, before drag/override/clamp/reset). Subtracts the
    /// <see cref="ExternalVelocity"/> the compose system added so every downstream modifier sees locomotion only —
    /// THIS is what stops a brake from eating a knockback and a knockback from being clamped to walking speed — then
    /// decays the external channel on its own constant so the hit fades regardless of any active brake.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsModifierGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PhysicsDragApplySystem))]
    [UpdateBefore(typeof(PhysicsModifierForceAccumulatorSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct PhysicsExternalVelocityDecomposeSystem : ISystem
    {
        private EntityQuery _query;
        private ComponentTypeHandle<PhysicsVelocity> _velocityHandle;
        private ComponentTypeHandle<ExternalVelocity> _externalHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            JobChunkWorkerBeginEndExtensions.EarlyJobInit<DecomposeJob>();

            _query = SystemAPI.QueryBuilder()
                .WithAllRW<PhysicsVelocity, ExternalVelocity>()
                .WithAll<LocalToWorld>()
                .Build();

            _velocityHandle = state.GetComponentTypeHandle<PhysicsVelocity>();
            _externalHandle = state.GetComponentTypeHandle<ExternalVelocity>();

            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _velocityHandle.Update(ref state);
            _externalHandle.Update(ref state);

            var dt = SystemAPI.Time.DeltaTime;
            var rate = math.max(1e-3f, ExternalVelocityConfig.DecayRate.Data);
            var decay = dt > 0f ? math.exp(-rate * dt) : 1f;

            state.Dependency = new DecomposeJob
            {
                VelocityHandle = _velocityHandle,
                ExternalHandle = _externalHandle,
                Decay = decay,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private struct DecomposeJob : IJobChunkWorkerBeginEnd
        {
            public ComponentTypeHandle<PhysicsVelocity> VelocityHandle;
            public ComponentTypeHandle<ExternalVelocity> ExternalHandle;
            public float Decay;

            private const float RestEpsilonSq = 1e-6f;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var velocities = chunk.GetNativeArray(ref VelocityHandle);
                var externals = chunk.GetNativeArray(ref ExternalHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var i))
                {
                    var external = externals[i];
                    if (math.lengthsq(external.Linear) <= 0f && math.lengthsq(external.Angular) <= 0f)
                    {
                        continue;
                    }

                    var velocity = velocities[i];
                    velocity.Linear -= external.Linear;
                    velocity.Angular -= external.Angular;
                    velocities[i] = velocity;

                    external.Linear *= Decay;
                    external.Angular *= Decay;
                    if (math.lengthsq(external.Linear) < RestEpsilonSq)
                    {
                        external.Linear = float3.zero;
                    }

                    if (math.lengthsq(external.Angular) < RestEpsilonSq)
                    {
                        external.Angular = float3.zero;
                    }

                    externals[i] = external;
                }
            }
        }
    }
}
