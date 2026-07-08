namespace BovineLabs.Timeline.Physics.Tests
{
    using BovineLabs.Nerve.PhysicsStates;
    using BovineLabs.Testing;
    using BovineLabs.Timeline.Physics.Bindings;
    using NUnit.Framework;
    using Unity.Core;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    /// <summary>
    /// Verifies the optional game-side binding drivers that feed the ZoneStateGate, LightExposureGate and
    /// TauntOverride extension points of <see cref="PhysicsTriggerQueryData"/>.
    /// </summary>
    public class TriggerQueryBindingSystemsTests : ECSTestsFixture
    {
        // ---- Zone ----

        [Test]
        public void ZoneVolumeSystem_EnablesTag_WhileOverlappingZoneVolume()
        {
            var zone = Manager.CreateEntity();
            Manager.AddComponent<TriggerQueryZoneVolume>(zone);

            var body = CreateZoneMember();
            AddTriggerEvent(body, zone, StatefulEventState.Enter);

            RunZoneSystem();

            Assert.IsTrue(Manager.IsComponentEnabled<TriggerQueryZoneTag>(body),
                "a body overlapping a zone volume must have its tag enabled");
        }

        [Test]
        public void ZoneVolumeSystem_LeavesTagDisabled_WhenOverlapIsNotAZoneVolume()
        {
            var notAZone = Manager.CreateEntity(); // carries no TriggerQueryZoneVolume marker

            var body = CreateZoneMember();
            AddTriggerEvent(body, notAZone, StatefulEventState.Stay);

            RunZoneSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<TriggerQueryZoneTag>(body),
                "overlapping a non-zone body must not enable the tag");
        }

        [Test]
        public void ZoneVolumeSystem_DisablesTag_OnZoneExit()
        {
            var zone = Manager.CreateEntity();
            Manager.AddComponent<TriggerQueryZoneVolume>(zone);

            var body = CreateZoneMember();
            Manager.SetComponentEnabled<TriggerQueryZoneTag>(body, true); // was inside last frame
            AddTriggerEvent(body, zone, StatefulEventState.Exit);

            RunZoneSystem();

            Assert.IsFalse(Manager.IsComponentEnabled<TriggerQueryZoneTag>(body),
                "an Exit-only overlap must disable the tag");
        }

        // ---- Light exposure ----

        [Test]
        public void ExposureSystem_WritesLinearFalloff_FromSingleSource()
        {
            CreateExposureSource(float3.zero, intensity: 1f, range: 10f);
            var body = CreateExposedBody(new float3(0, 0, 5)); // halfway → 0.5

            RunExposureSystem();

            Assert.AreEqual(0.5f, Manager.GetComponentData<TriggerQueryExposure>(body).Value, 1e-4f);
        }

        [Test]
        public void ExposureSystem_SumsSources_AndDropsOutOfRange()
        {
            CreateExposureSource(float3.zero, intensity: 1f, range: 10f); // at dist 5 → 0.5
            CreateExposureSource(new float3(0, 0, 10), intensity: 2f, range: 10f); // at dist 5 → 1.0
            CreateExposureSource(new float3(100, 0, 0), intensity: 5f, range: 4f); // far → 0
            var body = CreateExposedBody(new float3(0, 0, 5));

            RunExposureSystem();

            Assert.AreEqual(1.5f, Manager.GetComponentData<TriggerQueryExposure>(body).Value, 1e-4f);
        }

        [Test]
        public void ExposureSystem_WritesZero_WhenNoSources()
        {
            var body = CreateExposedBody(new float3(0, 0, 5));
            Manager.SetComponentData(body, new TriggerQueryExposure { Value = 99f });

            RunExposureSystem();

            Assert.AreEqual(0f, Manager.GetComponentData<TriggerQueryExposure>(body).Value, 1e-4f);
        }

        // ---- Taunt ----

        [Test]
        public void TauntSystem_StampsUntilTime_FromFixedStepElapsed_AndConsumesRequest()
        {
            World.SetTime(new TimeData(elapsedTime: 12.0, deltaTime: 1f / 60f));

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new TriggerQueryTaunt());
            Manager.AddComponentData(body, new TriggerQueryTauntRequest { Duration = 3f });
            Manager.SetComponentEnabled<TriggerQueryTauntRequest>(body, true);

            RunTauntSystem();

            Assert.AreEqual(15f, Manager.GetComponentData<TriggerQueryTaunt>(body).UntilTime, 1e-4f,
                "UntilTime must be SystemAPI.Time.ElapsedTime + Duration on the same clock the query compares");
            Assert.IsFalse(Manager.IsComponentEnabled<TriggerQueryTauntRequest>(body),
                "the request must be consumed so it fires once per enable");

            // The stamped taunt must read as unexpired to the query's `UntilTime > ElapsedTime` test now, and expired later.
            Assert.Greater(Manager.GetComponentData<TriggerQueryTaunt>(body).UntilTime, 12f);
            Assert.Less(Manager.GetComponentData<TriggerQueryTaunt>(body).UntilTime, 16f);
        }

        [Test]
        public void TauntSystem_LeavesUntilTimeUntouched_WhenRequestDisabled()
        {
            World.SetTime(new TimeData(elapsedTime: 5.0, deltaTime: 1f / 60f));

            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new TriggerQueryTaunt());
            Manager.AddComponentData(body, new TriggerQueryTauntRequest { Duration = 3f });
            Manager.SetComponentEnabled<TriggerQueryTauntRequest>(body, false);

            RunTauntSystem();

            Assert.AreEqual(0f, Manager.GetComponentData<TriggerQueryTaunt>(body).UntilTime, 1e-4f,
                "a disabled request must not stamp a taunt");
        }

        // ---- helpers ----

        private Entity CreateZoneMember()
        {
            var body = Manager.CreateEntity();
            Manager.AddBuffer<StatefulTriggerEvent>(body);
            Manager.AddComponent<TriggerQueryZoneTag>(body);
            Manager.SetComponentEnabled<TriggerQueryZoneTag>(body, false);
            return body;
        }

        private void AddTriggerEvent(Entity body, Entity other, StatefulEventState state)
        {
            Manager.GetBuffer<StatefulTriggerEvent>(body).Add(new StatefulTriggerEvent
            {
                EntityB = other,
                State = state,
            });
        }

        private Entity CreateExposureSource(float3 position, float intensity, float range)
        {
            var source = Manager.CreateEntity();
            Manager.AddComponentData(source, new LocalToWorld { Value = float4x4.Translate(position) });
            Manager.AddComponentData(source, new TriggerExposureSource { Intensity = intensity, Range = range });
            return source;
        }

        private Entity CreateExposedBody(float3 position)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.Translate(position) });
            Manager.AddComponentData(body, new TriggerQueryExposure());
            return body;
        }

        private void RunZoneSystem()
        {
            World.GetOrCreateSystem<TriggerQueryZoneVolumeSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunExposureSystem()
        {
            World.GetOrCreateSystem<TriggerQueryExposureSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        private void RunTauntSystem()
        {
            World.GetOrCreateSystem<TriggerQueryTauntSystem>().Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }
    }
}
