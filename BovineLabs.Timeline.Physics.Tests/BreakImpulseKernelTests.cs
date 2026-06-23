using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class BreakImpulseKernelTests
    {
        [Test]
        public void Brake_ZeroRestitution_ReturnsNegativeVelocity()
        {
            var v = new float3(3f, -2f, 5f);

            var deltaV = BreakImpulseKernel.ComputeDeltaV(PhysicsBreakMode.Brake, quaternion.identity, v,
                math.length(v), 0f, 0f, 0f);

            Assert.AreEqual(-v.x, deltaV.x, 1e-5f);
            Assert.AreEqual(-v.y, deltaV.y, 1e-5f);
            Assert.AreEqual(-v.z, deltaV.z, 1e-5f);
        }

        [Test]
        public void Brake_FullRestitution_ReturnsNegativeTwiceVelocity()
        {
            var v = new float3(3f, -2f, 5f);

            var deltaV = BreakImpulseKernel.ComputeDeltaV(PhysicsBreakMode.Brake, quaternion.identity, v,
                math.length(v), 0f, 0f, 1f);

            Assert.AreEqual(-2f * v.x, deltaV.x, 1e-5f);
            Assert.AreEqual(-2f * v.y, deltaV.y, 1e-5f);
            Assert.AreEqual(-2f * v.z, deltaV.z, 1e-5f);
        }

        [Test]
        public void Redirect_IdentityZeroAngles_AimsForward()
        {
            var v = new float3(1f, 2f, 3f);
            var speed = math.length(v);
            var restitution = 0.5f;

            var deltaV = BreakImpulseKernel.ComputeDeltaV(PhysicsBreakMode.Redirect, quaternion.identity, v, speed, 0f,
                0f, restitution);

            var expected = new float3(0f, 0f, restitution * speed) - v;
            Assert.AreEqual(expected.x, deltaV.x, 1e-5f);
            Assert.AreEqual(expected.y, deltaV.y, 1e-5f);
            Assert.AreEqual(expected.z, deltaV.z, 1e-5f);
        }

        [Test]
        public void Redirect_AzimuthHalfPi_AimsRight()
        {
            var v = float3.zero;
            var speed = 4f;
            var restitution = 1f;

            var deltaV = BreakImpulseKernel.ComputeDeltaV(PhysicsBreakMode.Redirect, quaternion.identity, v, speed, 0f,
                math.PI / 2f, restitution);

            Assert.AreEqual(restitution * speed, deltaV.x, 1e-5f);
            Assert.AreEqual(0f, deltaV.y, 1e-5f);
            Assert.AreEqual(0f, deltaV.z, 1e-5f);
        }

        [Test]
        public void Redirect_KnownAngles_MatchesBasis()
        {
            var elevation = 0.6f;
            var azimuth = 1.1f;
            var speed = 7f;
            var restitution = 0.8f;

            var deltaV = BreakImpulseKernel.ComputeDeltaV(PhysicsBreakMode.Redirect, quaternion.identity, float3.zero,
                speed, elevation, azimuth, restitution);

            math.sincos(elevation, out var se, out var ce);
            math.sincos(azimuth, out var sa, out var ca);
            var localDir = new float3(sa * ce, se, ca * ce);
            var expected = localDir * (restitution * speed);

            Assert.AreEqual(expected.x, deltaV.x, 1e-5f);
            Assert.AreEqual(expected.y, deltaV.y, 1e-5f);
            Assert.AreEqual(expected.z, deltaV.z, 1e-5f);
        }
    }
}
