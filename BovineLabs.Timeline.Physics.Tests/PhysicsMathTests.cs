using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    [TestFixture]
    public class PhysicsMathTests
    {
        [Test]
        public void ComputeExponentialDecay_ZeroDt_ReturnsInput()
        {
            var vel = new PhysicsVelocity { Linear = new float3(1, 2, 3), Angular = new float3(4, 5, 6) };
            var drag = new PhysicsDragData { Linear = 2f, Angular = 3f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 0f, out var result);

            Assert.AreEqual(vel.Linear.x, result.Linear.x);
            Assert.AreEqual(vel.Linear.y, result.Linear.y);
            Assert.AreEqual(vel.Linear.z, result.Linear.z);
            Assert.AreEqual(vel.Angular.x, result.Angular.x);
            Assert.AreEqual(vel.Angular.y, result.Angular.y);
            Assert.AreEqual(vel.Angular.z, result.Angular.z);
        }

        [Test]
        public void ComputeExponentialDecay_NegativeDt_ReturnsInput()
        {
            var vel = new PhysicsVelocity { Linear = new float3(10, 0, 0), Angular = new float3(0, 10, 0) };
            var drag = new PhysicsDragData { Linear = 1f, Angular = 1f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, -1f, out var result);

            Assert.AreEqual(vel.Linear, result.Linear);
            Assert.AreEqual(vel.Angular, result.Angular);
        }

        [Test]
        public void ComputeExponentialDecay_ZeroDrag_NoDecay()
        {
            var vel = new PhysicsVelocity { Linear = new float3(5, 6, 7), Angular = new float3(8, 9, 10) };
            var drag = new PhysicsDragData { Linear = 0f, Angular = 0f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 1f, out var result);

            Assert.AreEqual(vel.Linear, result.Linear);
            Assert.AreEqual(vel.Angular, result.Angular);
        }

        [Test]
        public void ComputeExponentialDecay_PositiveDrag_DecaysLinear()
        {
            var vel = new PhysicsVelocity { Linear = new float3(100, 0, 0), Angular = float3.zero };
            var drag = new PhysicsDragData { Linear = 1f, Angular = 0f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 1f, out var result);

            Assert.Less(result.Linear.x, 100f);
            Assert.Greater(result.Linear.x, 0f);
        }

        [Test]
        public void ComputeExponentialDecay_PositiveDrag_DecaysAngular()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = new float3(0, 50, 0) };
            var drag = new PhysicsDragData { Linear = 0f, Angular = 2f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 1f, out var result);

            Assert.Less(result.Angular.y, 50f);
            Assert.Greater(result.Angular.y, 0f);
        }

        [Test]
        public void ComputeExponentialDecay_LargeDrag_NearlyZero()
        {
            var vel = new PhysicsVelocity { Linear = new float3(1, 1, 1), Angular = new float3(1, 1, 1) };
            var drag = new PhysicsDragData { Linear = 100f, Angular = 100f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 1f, out var result);

            Assert.Less(math.length(result.Linear), 0.01f);
            Assert.Less(math.length(result.Angular), 0.01f);
        }

        [Test]
        public void ComputeExponentialDecay_HigherDrag_MoreDecay()
        {
            var vel = new PhysicsVelocity { Linear = new float3(10, 0, 0), Angular = float3.zero };

            PhysicsMath.ComputeExponentialDecay(vel, new PhysicsDragData { Linear = 1f, Angular = 0f }, 1f, out var low);
            PhysicsMath.ComputeExponentialDecay(vel, new PhysicsDragData { Linear = 5f, Angular = 0f }, 1f, out var high);

            Assert.Less(high.Linear.x, low.Linear.x);
        }

        [Test]
        public void ComputeExponentialDecay_LongerDt_MoreDecay()
        {
            var vel = new PhysicsVelocity { Linear = new float3(10, 0, 0), Angular = float3.zero };
            var drag = new PhysicsDragData { Linear = 1f, Angular = 0f };

            PhysicsMath.ComputeExponentialDecay(vel, drag, 0.5f, out var shortDt);
            PhysicsMath.ComputeExponentialDecay(vel, drag, 2f, out var longDt);

            Assert.Less(longDt.Linear.x, shortDt.Linear.x);
        }

        // ── ComputePidForce ────────────────────────────────────────────────

        [Test]
        public void ComputePidForce_ZeroDt_ZeroOutput_UnchangedState()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), Derivative = new float3(1f), Integral = new float3(1f), MaxOutput = 100f };
            var state = new PidStateData { IsInitialized = true, PreviousError = new float3(1, 0, 0), IntegralAccumulator = float3.zero };

            PhysicsMath.ComputePidForce(new float3(5, 0, 0), tuning, state, 0f, out var output, out var next);

            Assert.AreEqual(float3.zero, output);
            Assert.IsTrue(next.IsInitialized);
            Assert.AreEqual(state.PreviousError, next.PreviousError);
            Assert.AreEqual(state.IntegralAccumulator, next.IntegralAccumulator);
        }

        [Test]
        public void ComputePidForce_NegativeDt_ZeroOutput()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), MaxOutput = 100f };
            var state = new PidStateData();

            PhysicsMath.ComputePidForce(new float3(5, 0, 0), tuning, state, -0.1f, out var output, out _);

            Assert.AreEqual(float3.zero, output);
        }

        [Test]
        public void ComputePidForce_Uninitialized_UsesCurrentErrorAsPrevious()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), Derivative = float3.zero, Integral = float3.zero, MaxOutput = 1000f };
            var state = new PidStateData { IsInitialized = false };

            PhysicsMath.ComputePidForce(new float3(10, 0, 0), tuning, state, 1f, out var output, out var next);

            Assert.IsTrue(next.IsInitialized);
            Assert.AreEqual(new float3(10, 0, 0), next.PreviousError);
            var expected = tuning.Proportional * new float3(10, 0, 0);
            Assert.AreEqual(expected.x, output.x, 0.001f);
            Assert.AreEqual(expected.y, output.y, 0.001f);
            Assert.AreEqual(expected.z, output.z, 0.001f);
        }

        [Test]
        public void ComputePidForce_Initialized_UsesStoredPreviousError()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), Derivative = new float3(1f), Integral = float3.zero, MaxOutput = 1000f };
            var state = new PidStateData { IsInitialized = true, PreviousError = new float3(5, 0, 0) };

            PhysicsMath.ComputePidForce(new float3(10, 0, 0), tuning, state, 1f, out var output, out var next);

            var derivTerm = (new float3(10, 0, 0) - new float3(5, 0, 0)) / 1f;
            var expected = tuning.Proportional * new float3(10, 0, 0) + tuning.Derivative * derivTerm;
            Assert.AreEqual(expected.x, output.x, 0.001f);
        }

        [Test]
        public void ComputePidForce_IntegralAccumulates()
        {
            var tuning = new PidTuning { Proportional = float3.zero, Derivative = float3.zero, Integral = new float3(2f), MaxOutput = 1000f };
            var state = new PidStateData { IsInitialized = true, IntegralAccumulator = new float3(3, 0, 0) };

            PhysicsMath.ComputePidForce(new float3(1, 0, 0), tuning, state, 1f, out _, out var next);

            Assert.AreEqual(new float3(4, 0, 0), next.IntegralAccumulator);
        }

        [Test]
        public void ComputePidForce_MaxOutput_ClampsForce()
        {
            var tuning = new PidTuning { Proportional = new float3(1000f), Derivative = float3.zero, Integral = float3.zero, MaxOutput = 5f };
            var state = new PidStateData();

            PhysicsMath.ComputePidForce(new float3(1, 0, 0), tuning, state, 1f, out var output, out _);

            Assert.LessOrEqual(math.length(output), 5f + 0.001f);
            Assert.Greater(math.length(output), 0f);
        }

        [Test]
        public void ComputePidForce_IntegralClamped_ByMaxOutput()
        {
            var tuning = new PidTuning { Proportional = float3.zero, Derivative = float3.zero, Integral = new float3(1f), MaxOutput = 1f };
            var state = new PidStateData { IsInitialized = true, IntegralAccumulator = new float3(100, 0, 0) };

            PhysicsMath.ComputePidForce(new float3(100, 0, 0), tuning, state, 1f, out _, out var next);

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral.x, 0.001f);
            Assert.LessOrEqual(math.abs(next.IntegralAccumulator.x), integralMax + 0.001f);
        }

        [Test]
        public void ComputePidForce_AllZeroGains_ZeroOutput()
        {
            var tuning = new PidTuning { Proportional = float3.zero, Derivative = float3.zero, Integral = float3.zero, MaxOutput = 100f };
            var state = new PidStateData();

            PhysicsMath.ComputePidForce(new float3(10, 20, 30), tuning, state, 1f, out var output, out _);

            Assert.AreEqual(float3.zero, output);
        }

        [Test]
        public void ComputePidForce_SetsIsInitialized()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), MaxOutput = 100f };
            var state = new PidStateData { IsInitialized = false };

            PhysicsMath.ComputePidForce(float3.zero, tuning, state, 1f, out _, out var next);

            Assert.IsTrue(next.IsInitialized);
        }

        [Test]
        public void ComputePidForce_NextStateStoresError()
        {
            var tuning = new PidTuning { Proportional = new float3(1f), MaxOutput = 100f };
            var state = new PidStateData();
            var error = new float3(3, 4, 5);

            PhysicsMath.ComputePidForce(error, tuning, state, 1f, out _, out var next);

            Assert.AreEqual(error, next.PreviousError);
        }

        // ── ComputeAngularError ────────────────────────────────────────────

        [Test]
        public void ComputeAngularError_SameRotation_ZeroError()
        {
            var q = quaternion.identity;

            PhysicsMath.ComputeAngularError(q, q, out var error);

            Assert.Less(math.length(error), 0.0001f);
        }

        [Test]
        public void ComputeAngularError_IdentityTo90DegYaw_NonZeroError()
        {
            PhysicsMath.ComputeAngularError(quaternion.identity, quaternion.RotateY(math.PI / 2f), out var error);

            Assert.Greater(math.length(error), 0.01f);
        }

        [Test]
        public void ComputeAngularError_90DegYaw_YAxisError()
        {
            PhysicsMath.ComputeAngularError(quaternion.identity, quaternion.RotateY(math.PI / 2f), out var error);

            Assert.Greater(math.abs(error.y), 0.01f);
            Assert.Less(math.abs(error.x), 0.01f);
            Assert.Less(math.abs(error.z), 0.01f);
        }

        [Test]
        public void ComputeAngularError_IdentityTo180_NonZero()
        {
            PhysicsMath.ComputeAngularError(quaternion.identity, quaternion.RotateY(math.PI), out var error);

            Assert.Greater(math.length(error), 0.1f);
        }

        [Test]
        public void ComputeAngularError_SmallAngle_SmallError()
        {
            PhysicsMath.ComputeAngularError(quaternion.identity, quaternion.RotateY(0.001f), out var error);

            Assert.Less(math.length(error), 0.01f);
        }

        [Test]
        public void ComputeAngularError_IsAntisymmetric()
        {
            var a = quaternion.RotateX(0.5f);
            var b = quaternion.RotateZ(1.2f);

            PhysicsMath.ComputeAngularError(a, b, out var errAB);
            PhysicsMath.ComputeAngularError(b, a, out var errBA);

            Assert.AreEqual(-errAB.x, errBA.x, 0.001f);
            Assert.AreEqual(-errAB.y, errBA.y, 0.001f);
            Assert.AreEqual(-errAB.z, errBA.z, 0.001f);
        }

        // ── ApplyLinearForce ───────────────────────────────────────────────

        [Test]
        public void ApplyLinearForce_BasicForce_IncreasesVelocity()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass = CreateMass(1f);

            PhysicsMath.ApplyLinearForce(vel, mass, new float3(10, 0, 0), 1f, out var result);

            Assert.Greater(result.Linear.x, 0f);
            Assert.AreEqual(float3.zero, result.Angular);
        }

        [Test]
        public void ApplyLinearForce_ZeroForce_NoChange()
        {
            var vel = new PhysicsVelocity { Linear = new float3(5, 0, 0), Angular = float3.zero };
            var mass = CreateMass(1f);

            PhysicsMath.ApplyLinearForce(vel, mass, float3.zero, 1f, out var result);

            Assert.AreEqual(new float3(5, 0, 0), result.Linear);
        }

        [Test]
        public void ApplyLinearForce_ZeroDt_NoChange()
        {
            var vel = new PhysicsVelocity { Linear = new float3(3, 0, 0), Angular = float3.zero };
            var mass = CreateMass(1f);

            PhysicsMath.ApplyLinearForce(vel, mass, new float3(10, 0, 0), 0f, out var result);

            Assert.AreEqual(new float3(3, 0, 0), result.Linear);
        }

        [Test]
        public void ApplyLinearForce_InverseMass2_DoubleVelocityChange()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass1 = CreateMass(1f);
            var mass2 = CreateMass(2f);

            PhysicsMath.ApplyLinearForce(vel, mass1, new float3(10, 0, 0), 1f, out var result1);
            PhysicsMath.ApplyLinearForce(vel, mass2, new float3(10, 0, 0), 1f, out var result2);

            Assert.AreEqual(result1.Linear.x * 2f, result2.Linear.x, 0.001f);
        }

        [Test]
        public void ApplyLinearForce_ZeroInverseMass_UsesFallbackOne()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass = default(PhysicsMass);

            PhysicsMath.ApplyLinearForce(vel, mass, new float3(10, 0, 0), 1f, out var result);

            Assert.AreEqual(10f, result.Linear.x, 0.001f);
        }

        // ── ApplyAngularTorque ─────────────────────────────────────────────

        [Test]
        public void ApplyAngularTorque_Basic_IncreasesAngularVelocity()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass = CreateMass(1f);
            var transform = LocalTransform.Identity;

            PhysicsMath.ApplyAngularTorque(vel, mass, transform, new float3(0, 10, 0), 1f, out var result);

            Assert.Greater(math.abs(result.Angular.y), 0f);
            Assert.AreEqual(float3.zero, result.Linear);
        }

        [Test]
        public void ApplyAngularTorque_ZeroTorque_NoChange()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = new float3(1, 2, 3) };
            var mass = CreateMass(1f);
            var transform = LocalTransform.Identity;

            PhysicsMath.ApplyAngularTorque(vel, mass, transform, float3.zero, 1f, out var result);

            Assert.AreEqual(new float3(1, 2, 3), result.Angular);
        }

        [Test]
        public void ApplyAngularTorque_ZeroInverseInertia_UsesFallback()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass = default(PhysicsMass);
            var transform = LocalTransform.Identity;

            PhysicsMath.ApplyAngularTorque(vel, mass, transform, new float3(0, 5, 0), 1f, out var result);

            Assert.Greater(math.abs(result.Angular.y), 0f);
        }

        [Test]
        public void ApplyAngularTorque_RotatedTransform_RotatesTorque()
        {
            var vel = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var mass = CreateMass(1f);
            var transform = LocalTransform.FromPositionRotation(float3.zero, quaternion.RotateY(math.PI / 2f));

            PhysicsMath.ApplyAngularTorque(vel, mass, transform, new float3(0, 10, 0), 1f, out var result);

            Assert.Greater(math.length(result.Angular), 0f);
        }

        private static PhysicsMass CreateMass(float inverseMass)
        {
            var m = default(PhysicsMass);
            m.InverseMass = inverseMass;
            m.InverseInertia = new float3(inverseMass);
            return m;
        }
    }
}
