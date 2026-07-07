namespace BovineLabs.Timeline.Physics.Bindings
{
    using BovineLabs.Timeline.Physics.Infrastructure;
    using BovineLabs.Timeline.Physics.TriggerEvents;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    /// <summary>
    /// Reference driver for the LightExposureGate mechanism: writes each body's <see cref="TriggerQueryExposure.Value"/>
    /// as the summed illumination from every authored <see cref="TriggerExposureSource"/>, using a linear
    /// distance falloff (contribution = Intensity * saturate(1 - distance / Range)). This is a deliberately minimal,
    /// deterministic, occlusion-free model so the gate has a source of truth without a full lighting system; a game
    /// that already computes illumination should write <c>Value</c> from that instead and delete this system.
    /// Runs in <see cref="PhysicsProducerGroup"/> before <see cref="PhysicsTriggerQuerySystem"/> so the value is
    /// current for the gate the same fixed step.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsProducerGroup))]
    [UpdateBefore(typeof(PhysicsTriggerQuerySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ServerSimulation)]
    [BurstCompile]
    public partial struct TriggerQueryExposureSystem : ISystem
    {
        private EntityQuery _sourceQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _sourceQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<TriggerExposureSource, LocalToWorld>()
                .Build(ref state);

            state.RequireForUpdate<TriggerQueryExposure>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var sources = _sourceQuery.ToComponentDataArray<TriggerExposureSource>(state.WorldUpdateAllocator);
            var sourceTransforms = _sourceQuery.ToComponentDataArray<LocalToWorld>(state.WorldUpdateAllocator);

            state.Dependency = new ExposureJob
            {
                Sources = sources,
                SourceTransforms = sourceTransforms,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ExposureJob : IJobEntity
        {
            [ReadOnly]
            public NativeArray<TriggerExposureSource> Sources;

            [ReadOnly]
            public NativeArray<LocalToWorld> SourceTransforms;

            private void Execute(ref TriggerQueryExposure exposure, in LocalToWorld ltw)
            {
                var pos = ltw.Position;
                var total = 0f;

                for (var i = 0; i < Sources.Length; i++)
                {
                    var source = Sources[i];
                    if (source.Range <= 0f)
                    {
                        continue;
                    }

                    var distance = math.distance(pos, SourceTransforms[i].Position);
                    total += source.Intensity * math.saturate(1f - (distance / source.Range));
                }

                exposure.Value = total;
            }
        }
    }
}
