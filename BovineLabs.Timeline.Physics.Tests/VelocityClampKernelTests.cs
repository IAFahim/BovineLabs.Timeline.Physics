using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class VelocityClampKernelTests
    {
        [Test]
        public void UnderMax_PassesThroughUnchanged()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = new float3(1f, 2f, 2f),
                Angular = new float3(0f, 1f, 0f)
            };

            var result = VelocityClampKernel.Clamp(velocity, 10f, 10f);

            Assert.AreEqual(velocity.Linear.x, result.Linear.x, 1e-5f);
            Assert.AreEqual(velocity.Linear.y, result.Linear.y, 1e-5f);
            Assert.AreEqual(velocity.Linear.z, result.Linear.z, 1e-5f);
            Assert.AreEqual(velocity.Angular.y, result.Angular.y, 1e-5f);
        }

        [Test]
        public void OverMaxLinear_ClampsToMagnitudeAndKeepsDirection()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = new float3(30f, 0f, 0f),
                Angular = float3.zero
            };

            var result = VelocityClampKernel.Clamp(velocity, 5f, -1f);

            Assert.AreEqual(5f, math.length(result.Linear), 1e-5f);
            Assert.AreEqual(5f, result.Linear.x, 1e-5f);
            Assert.AreEqual(0f, result.Linear.y, 1e-5f);
            Assert.AreEqual(0f, result.Linear.z, 1e-5f);
        }

        [Test]
        public void OverMaxAngular_ClampsToMagnitude()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = new float3(0f, 12f, 0f)
            };

            var result = VelocityClampKernel.Clamp(velocity, -1f, 3f);

            Assert.AreEqual(3f, math.length(result.Angular), 1e-5f);
            Assert.AreEqual(3f, result.Angular.y, 1e-5f);
        }

        [Test]
        public void NegativeMaxLinear_LeavesLinearUntouched()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = new float3(100f, 100f, 100f),
                Angular = float3.zero
            };

            var result = VelocityClampKernel.Clamp(velocity, -1f, 10f);

            Assert.AreEqual(velocity.Linear.x, result.Linear.x, 1e-5f);
            Assert.AreEqual(velocity.Linear.y, result.Linear.y, 1e-5f);
            Assert.AreEqual(velocity.Linear.z, result.Linear.z, 1e-5f);
        }

        [Test]
        public void NegativeMaxAngular_LeavesAngularUntouched()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = new float3(50f, 50f, 50f)
            };

            var result = VelocityClampKernel.Clamp(velocity, 10f, -1f);

            Assert.AreEqual(velocity.Angular.x, result.Angular.x, 1e-5f);
            Assert.AreEqual(velocity.Angular.y, result.Angular.y, 1e-5f);
            Assert.AreEqual(velocity.Angular.z, result.Angular.z, 1e-5f);
        }

        [Test]
        public void ZeroVelocity_WithPositiveMax_PassesThrough()
        {
            var velocity = new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            };

            var result = VelocityClampKernel.Clamp(velocity, 5f, 5f);

            Assert.AreEqual(0f, math.length(result.Linear), 1e-5f);
            Assert.AreEqual(0f, math.length(result.Angular), 1e-5f);
        }
    }
}
