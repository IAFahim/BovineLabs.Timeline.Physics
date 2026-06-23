using BovineLabs.Timeline.Physics.Data.Forces;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class ForceInertiaKernelTests
    {
        private static PhysicsMass IdentityMass(float inverseMass, float3 inverseInertia)
        {
            return new PhysicsMass
            {
                Transform = RigidTransform.identity,
                InverseMass = inverseMass,
                InverseInertia = inverseInertia,
                AngularExpansionFactor = 0f,
            };
        }

        private static LocalToWorld IdentityTransform()
        {
            return new LocalToWorld { Value = float4x4.identity };
        }

        [Test]
        public void PureLinearForce_AddsScaledLinear_LeavesAngularUnchanged()
        {
            var velocity = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var totalLinear = new float3(2f, -4f, 6f);
            var mass = IdentityMass(0.5f, new float3(1f, 1f, 1f));

            var result = ForceInertiaKernel.ApplyForcesToVelocity(velocity, totalLinear, float3.zero, mass,
                IdentityTransform());

            Assert.AreEqual(1f, result.Linear.x, 1e-5f);
            Assert.AreEqual(-2f, result.Linear.y, 1e-5f);
            Assert.AreEqual(3f, result.Linear.z, 1e-5f);
            Assert.AreEqual(0f, result.Angular.x, 1e-5f);
            Assert.AreEqual(0f, result.Angular.y, 1e-5f);
            Assert.AreEqual(0f, result.Angular.z, 1e-5f);
        }

        [Test]
        public void KnownAngular_IdentityRotationUnitInertia_RoundTripsToAngularTimesInverseInertia()
        {
            var velocity = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var totalAngular = new float3(0.7f, -1.3f, 2.5f);
            var inverseInertia = new float3(1f, 1f, 1f);
            var mass = IdentityMass(1f, inverseInertia);

            var result = ForceInertiaKernel.ApplyForcesToVelocity(velocity, float3.zero, totalAngular, mass,
                IdentityTransform());

            Assert.AreEqual(totalAngular.x, result.Angular.x, 1e-5f);
            Assert.AreEqual(totalAngular.y, result.Angular.y, 1e-5f);
            Assert.AreEqual(totalAngular.z, result.Angular.z, 1e-5f);
        }

        [Test]
        public void OrthonormalizeInversePath_InvariantUnderIdentityRotation()
        {
            var velocity = new PhysicsVelocity { Linear = float3.zero, Angular = float3.zero };
            var totalAngular = new float3(3f, 5f, -2f);
            var inverseInertia = new float3(0.25f, 2f, 0.5f);
            var mass = IdentityMass(1f, inverseInertia);

            var result = ForceInertiaKernel.ApplyForcesToVelocity(velocity, float3.zero, totalAngular, mass,
                IdentityTransform());

            var expected = totalAngular * inverseInertia;
            Assert.AreEqual(expected.x, result.Angular.x, 1e-5f);
            Assert.AreEqual(expected.y, result.Angular.y, 1e-5f);
            Assert.AreEqual(expected.z, result.Angular.z, 1e-5f);
        }
    }
}
