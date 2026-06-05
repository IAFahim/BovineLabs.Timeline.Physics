namespace BovineLabs.Timeline.Physics.Tests
{
    using BovineLabs.Testing;
    using BovineLabs.Timeline.Physics.Forces;
    using NUnit.Framework;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;
    using Unity.Transforms;

    public class PhysicsForceAccumulatorSystemTests : ECSTestsFixture
    {
        [Test]
        public void AccumulatesPendingForcesIntoVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1, 1, 1), Transform = RigidTransform.identity });
            
            var forceBuffer = Manager.AddBuffer<PendingForce>(target);
            forceBuffer.Add(new PendingForce { Linear = new float3(0, 10, 0), Angular = new float3(0, 0, 0) });
            forceBuffer.Add(new PendingForce { Linear = new float3(5, 0, 0), Angular = new float3(1, 0, 0) });

            Manager.AddBuffer<PendingVelocity>(target);

            var sys = WorldExtensions.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>(World);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(5f, vel.Linear.x, 0.001f);
            Assert.AreEqual(10f, vel.Linear.y, 0.001f);
            Assert.AreEqual(0f, vel.Linear.z, 0.001f);
            
            Assert.AreEqual(1f, vel.Angular.x, 0.001f);

            var postForceBuffer = Manager.GetBuffer<PendingForce>(target);
            Assert.AreEqual(0, postForceBuffer.Length, "PendingForce buffer should be drained");
        }

        [Test]
        public void AccumulatesPendingVelocitiesIntoVelocity()
        {
            var target = Manager.CreateEntity();
            Manager.AddComponentData(target, LocalTransform.Identity);
            Manager.AddComponentData(target, new LocalToWorld { Value = float4x4.identity });
            Manager.AddComponentData(target, new PhysicsVelocity { Linear = new float3(1, 1, 1), Angular = float3.zero });
            Manager.AddComponentData(target, new PhysicsMass { InverseMass = 1f, InverseInertia = new float3(1, 1, 1), Transform = RigidTransform.identity });
            
            Manager.AddBuffer<PendingForce>(target);

            var velBuffer = Manager.AddBuffer<PendingVelocity>(target);
            velBuffer.Add(new PendingVelocity { Linear = new float3(0, 10, 0), Angular = new float3(0, 0, 0) });
            velBuffer.Add(new PendingVelocity { Linear = new float3(5, 0, 0), Angular = new float3(0, 2, 0) });

            var sys = WorldExtensions.GetOrCreateSystem<PhysicsProducerForceAccumulatorSystem>(World);
            sys.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();

            var vel = Manager.GetComponentData<PhysicsVelocity>(target);
            Assert.AreEqual(6f, vel.Linear.x, 0.001f);
            Assert.AreEqual(11f, vel.Linear.y, 0.001f);
            Assert.AreEqual(1f, vel.Linear.z, 0.001f);

            Assert.AreEqual(2f, vel.Angular.y, 0.001f);

            var postVelBuffer = Manager.GetBuffer<PendingVelocity>(target);
            Assert.AreEqual(0, postVelBuffer.Length, "PendingVelocity buffer should be drained");
        }
    }
}
