using BovineLabs.Testing;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsTrackSystemTests : ECSTestsFixture
    {

        [Test]
        public void ActiveVelocity_EnableableComponent()
        {
            var entity = Manager.CreateEntity(typeof(ActiveVelocity));
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveVelocity>(entity));

            Manager.SetComponentEnabled<ActiveVelocity>(entity, false);
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveVelocity>(entity));
        }

        [Test]
        public void ActiveDrag_EnableableComponent()
        {
            var entity = Manager.CreateEntity(typeof(ActiveDrag));
            Assert.IsTrue(Manager.IsComponentEnabled<ActiveDrag>(entity));

            Manager.SetComponentEnabled<ActiveDrag>(entity, false);
            Assert.IsFalse(Manager.IsComponentEnabled<ActiveDrag>(entity));
        }

        [Test]
        public void PhysicsVelocityState_DefaultNotFired()
        {
            var state = new PhysicsVelocityState();
            Assert.IsFalse(state.Fired);
        }

        [Test]
        public void PhysicsForceState_DefaultNotFired()
        {
            var state = new PhysicsForceState();
            Assert.IsFalse(state.Fired);
        }

        [Test]
        public void ComputeExponentialDecay_KnownValue()
        {
            var vel = new PhysicsVelocity { Linear = new float3(1, 0, 0), Angular = float3.zero };
            var drag = new PhysicsDragData { Linear = 1f, Angular = 0f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 1f, 1f, out var result);

            Assert.AreEqual(math.exp(-1f), result.Linear.x, 0.0001f);
            Assert.AreEqual(0f, result.Linear.y, 0.0001f);
            Assert.AreEqual(0f, result.Linear.z, 0.0001f);
        }

        [Test]
        public void ComputeAngularError_SameRotation_ZeroError()
        {
            PhysicsMath.ComputeAngularError(quaternion.identity, quaternion.identity, out var error);
            Assert.AreEqual(float3.zero, error);
        }

        [Test]
        public void ComputePidForce_ZeroDt_ZeroOutput()
        {
            var tuning = new PidTuning
                { Proportional = float3.zero, Integral = float3.zero, Derivative = float3.zero, MaxOutput = 0f };
            var state = new PidStateData();

            PhysicsMath.ComputePidForce(new float3(1, 2, 3), tuning, state, 0f, out var output, out var nextState);

            Assert.AreEqual(float3.zero, output);
            Assert.IsFalse(nextState.IsInitialized);
        }
    }
}