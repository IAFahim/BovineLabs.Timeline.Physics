using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Forces;
using BovineLabs.Timeline.Physics.Kinematics;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PendingVelocityResetTests : ECSTestsFixture
    {
        private Entity CreateBody(float3 linearVelocity, float3 angularVelocity)
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsVelocity { Linear = linearVelocity, Angular = angularVelocity });
            Manager.AddComponentData(body,
                new PhysicsMass
                    { InverseMass = 1f, InverseInertia = new float3(1, 1, 1), Transform = RigidTransform.identity });
            Manager.AddComponent<PendingVelocityReset>(body);
            Manager.SetComponentEnabled<PendingVelocityReset>(body, false);
            return body;
        }

        private void RunAccumulator()
        {
            var sys = World.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>();
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
        }

        [Test]
        public void LinearReset_ZeroesLinearBeforeDrain_AngularUntouched()
        {
            var body = CreateBody(new float3(3, 4, 5), new float3(1, 2, 3));
            Manager.AddBuffer<PendingForce>(body).Add(new PendingForce { Linear = new float3(0, 0, 10) });
            Manager.AddBuffer<PendingVelocity>(body);

            Manager.SetComponentData(body, new PendingVelocityReset { Flags = VelocityResetFlags.Linear });
            Manager.SetComponentEnabled<PendingVelocityReset>(body, true);

            RunAccumulator();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(0f, velocity.Linear.x, 1e-5f);
            Assert.AreEqual(0f, velocity.Linear.y, 1e-5f);
            Assert.AreEqual(10f, velocity.Linear.z, 1e-5f);

            Assert.AreEqual(1f, velocity.Angular.x, 1e-5f);
            Assert.AreEqual(2f, velocity.Angular.y, 1e-5f);
            Assert.AreEqual(3f, velocity.Angular.z, 1e-5f);

            Assert.IsFalse(Manager.IsComponentEnabled<PendingVelocityReset>(body),
                "Reset request must be consumed exactly once");
            Assert.AreEqual(VelocityResetFlags.None, Manager.GetComponentData<PendingVelocityReset>(body).Flags);
        }

        [Test]
        public void BothReset_ZeroesEverything_EvenWithoutPendingEntries()
        {
            var body = CreateBody(new float3(3, 4, 5), new float3(1, 2, 3));
            Manager.AddBuffer<PendingForce>(body);
            Manager.AddBuffer<PendingVelocity>(body);

            Manager.SetComponentData(body, new PendingVelocityReset { Flags = VelocityResetFlags.Both });
            Manager.SetComponentEnabled<PendingVelocityReset>(body, true);

            RunAccumulator();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(0f, math.length(velocity.Linear), 1e-5f);
            Assert.AreEqual(0f, math.length(velocity.Angular), 1e-5f);
        }

        [Test]
        public void DashImpulse_WithLinearReset_TravelsIdenticallyEveryTime()
        {
            var body = CreateBody(new float3(7, 0, -2), float3.zero);
            Manager.AddBuffer<PendingForce>(body);
            Manager.AddBuffer<PendingVelocity>(body);

            Manager.AddComponentData(body, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Impulse,
                    DirectionMode = PhysicsForceDirectionMode.FixedVector,
                    Linear = new float3(0, 0, 10),
                    ResetVelocityOnFire = VelocityResetFlags.Linear
                }
            });
            Manager.AddComponentData(body, new PhysicsForceState());

            var kinematics = World.GetOrCreateSystem<PhysicsKinematicsApplySystem>();

            kinematics.Update(WorldUnmanaged);
            RunAccumulator();

            var firstDash = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(0f, firstDash.x, 1e-5f);
            Assert.AreEqual(0f, firstDash.y, 1e-5f);
            Assert.AreEqual(10f, firstDash.z, 1e-5f);

            var staleVelocity = Manager.GetComponentData<PhysicsVelocity>(body);
            staleVelocity.Linear = new float3(-4, 9, 1);
            Manager.SetComponentData(body, staleVelocity);
            Manager.SetComponentData(body, new PhysicsForceState());

            kinematics.Update(WorldUnmanaged);
            RunAccumulator();

            var secondDash = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(firstDash.x, secondDash.x, 1e-5f);
            Assert.AreEqual(firstDash.y, secondDash.y, 1e-5f);
            Assert.AreEqual(firstDash.z, secondDash.z, 1e-5f);
        }

        [Test]
        public void DashImpulse_WithoutReset_InheritsStaleVelocity()
        {
            var body = CreateBody(new float3(7, 0, 0), float3.zero);
            Manager.AddBuffer<PendingForce>(body);
            Manager.AddBuffer<PendingVelocity>(body);

            Manager.AddComponentData(body, new ActiveForce
            {
                Config = new PhysicsForceData
                {
                    Mode = PhysicsForceMode.Impulse,
                    DirectionMode = PhysicsForceDirectionMode.FixedVector,
                    Linear = new float3(0, 0, 10)
                }
            });
            Manager.AddComponentData(body, new PhysicsForceState());

            World.GetOrCreateSystem<PhysicsKinematicsApplySystem>().Update(WorldUnmanaged);
            RunAccumulator();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body).Linear;
            Assert.AreEqual(7f, velocity.x, 1e-5f, "Without reset the stale momentum stacks — the reported bug");
            Assert.AreEqual(10f, velocity.z, 1e-5f);
        }

        [Test]
        public void PendingVelocityOnlyEntity_IsDrained()
        {
            var body = Manager.CreateEntity();
            Manager.AddComponentData(body, LocalTransform.Identity);
            Manager.AddComponentData(body, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(body, new PhysicsVelocity { Linear = new float3(1, 0, 0) });

            var deltas = Manager.AddBuffer<PendingVelocity>(body);
            deltas.Add(new PendingVelocity { Linear = new float3(0, 0, 6), Angular = new float3(0, 2, 0) });

            RunAccumulator();

            var velocity = Manager.GetComponentData<PhysicsVelocity>(body);
            Assert.AreEqual(1f, velocity.Linear.x, 1e-5f);
            Assert.AreEqual(6f, velocity.Linear.z, 1e-5f);
            Assert.AreEqual(2f, velocity.Angular.y, 1e-5f);
            Assert.AreEqual(0, Manager.GetBuffer<PendingVelocity>(body).Length,
                "Entities without a PendingForce buffer must still be drained");
        }
    }
}
