using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Kinematics;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsForceDirectionModeTests : ECSTestsFixture
    {
        private Entity CreateForceBody(PhysicsForceData config)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new ActiveForce { Config = config });
            Manager.AddComponentData(body, new PhysicsForceState());
            Manager.AddComponentData(body, new PhysicsForceRandom());
            Manager.AddBuffer<PendingForce>(body);
            return body;
        }

        private float3 RunAndReadForce(Entity body)
        {
            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var pending = Manager.GetBuffer<PendingForce>(body);
            Assert.AreEqual(1, pending.Length, "Expected exactly one pending force");
            return pending[0].Linear;
        }

        [Test]
        public void RandomSphere_IsDeterministicForSeedAndEntity()
        {
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.RandomSphere,
                Magnitude = 5f,
                Seed = 1234,
                LatchDirection = true
            });

            var first = RunAndReadForce(body);
            Assert.AreEqual(5f, math.length(first), 1e-4f, "Random direction must be unit length times Magnitude");

            Manager.GetBuffer<PendingForce>(body).Clear();
            Manager.SetComponentData(body, new PhysicsForceState());
            Manager.SetComponentData(body, new PhysicsForceRandom());

            var second = RunAndReadForce(body);

            Assert.AreEqual(first.x, second.x, 1e-6f);
            Assert.AreEqual(first.y, second.y, 1e-6f);
            Assert.AreEqual(first.z, second.z, 1e-6f);
        }

        [Test]
        public void RandomSphere_LatchHoldsDirectionAcrossFires()
        {
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.RandomSphere,
                Magnitude = 1f,
                Seed = 7,
                LatchDirection = true
            });

            var first = RunAndReadForce(body);

            Manager.GetBuffer<PendingForce>(body).Clear();
            var state = Manager.GetComponentData<PhysicsForceState>(body);
            state.Fired = false;
            Manager.SetComponentData(body, state);

            var second = RunAndReadForce(body);

            Assert.AreEqual(first.x, second.x, 1e-6f);
            Assert.AreEqual(first.y, second.y, 1e-6f);
            Assert.AreEqual(first.z, second.z, 1e-6f);
        }

        [Test]
        public void RandomSphere_UnlatchedResamplesAcrossFires()
        {
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.RandomSphere,
                Magnitude = 1f,
                Seed = 7,
                LatchDirection = false
            });

            var first = RunAndReadForce(body);

            Manager.GetBuffer<PendingForce>(body).Clear();
            var state = Manager.GetComponentData<PhysicsForceState>(body);
            state.Fired = false;
            Manager.SetComponentData(body, state);

            var second = RunAndReadForce(body);

            Assert.Greater(math.length(first - second), 1e-4f,
                "Unlatched random direction should advance the stream between fires");
        }

        [Test]
        public void RandomCone_ZeroRanges_PointsExactlyForward()
        {
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.RandomCone,
                Magnitude = 3f,
                Seed = 1,
                LatchDirection = true
            });

            var force = RunAndReadForce(body);

            Assert.AreEqual(0f, force.x, 1e-5f);
            Assert.AreEqual(0f, force.y, 1e-5f);
            Assert.AreEqual(3f, force.z, 1e-5f);
        }

        [Test]
        public void RandomCone_SampleStaysInsidePatch()
        {
            var azHalf = math.radians(45f);
            var elHalf = math.radians(20f);
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.RandomCone,
                Magnitude = 1f,
                ConeAzimuthHalfRange = azHalf,
                ConeElevationHalfRange = elHalf,
                Seed = 99,
                LatchDirection = true
            });

            var force = RunAndReadForce(body);
            var dir = math.normalize(force);

            var azimuth = math.atan2(dir.x, dir.z);
            var elevation = math.asin(math.clamp(dir.y, -1f, 1f));

            Assert.LessOrEqual(math.abs(azimuth), azHalf + 1e-4f);
            Assert.LessOrEqual(math.abs(elevation), elHalf + 1e-4f);
        }

        [Test]
        public void AlongVelocity_PushesWithMotion_AgainstVelocityOpposes()
        {
            var along = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.AlongVelocity,
                Magnitude = 4f,
                LatchDirection = false
            });
            Manager.AddComponentData(along, new PhysicsVelocity { Linear = new float3(0, 0, 5) });

            var against = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.AgainstVelocity,
                Magnitude = 4f,
                LatchDirection = false
            });
            Manager.AddComponentData(against, new PhysicsVelocity { Linear = new float3(0, 0, 5) });

            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var alongForce = Manager.GetBuffer<PendingForce>(along)[0].Linear;
            var againstForce = Manager.GetBuffer<PendingForce>(against)[0].Linear;

            Assert.AreEqual(4f, alongForce.z, 1e-5f);
            Assert.AreEqual(0f, alongForce.x, 1e-5f);
            Assert.AreEqual(-4f, againstForce.z, 1e-5f);
        }

        [Test]
        public void AlongVelocity_StationaryBody_DefersFire()
        {
            var body = CreateForceBody(new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                DirectionMode = PhysicsForceDirectionMode.AlongVelocity,
                Magnitude = 4f,
                LatchDirection = false
            });
            Manager.AddComponentData(body, new PhysicsVelocity());

            var sys = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            Assert.AreEqual(0, Manager.GetBuffer<PendingForce>(body).Length,
                "Velocity-relative force has no direction while stationary");
            Assert.IsFalse(Manager.GetComponentData<PhysicsForceState>(body).Fired,
                "Impulse stays armed until a direction is defined");
        }
    }
}