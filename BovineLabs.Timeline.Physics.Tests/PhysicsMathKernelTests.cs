using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Teleports;
using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class PhysicsMathKernelTests
    {
        [Test]
        public void Falloff_None_IsAlwaysOne()
        {
            Assert.AreEqual(1f, PhysicsMath.ComputeFalloff(PhysicsTriggerFalloffCurve.None, 100f, 1f, 2f));
        }

        [Test]
        public void Falloff_AnyCurve_IsFullInsideStartAndZeroOutsideEnd()
        {
            foreach (var curve in new[]
                     {
                         PhysicsTriggerFalloffCurve.Linear,
                         PhysicsTriggerFalloffCurve.InverseSquare,
                         PhysicsTriggerFalloffCurve.Step
                     })
            {
                Assert.AreEqual(1f, PhysicsMath.ComputeFalloff(curve, 0.5f, 1f, 4f), 0f, curve.ToString());
                Assert.AreEqual(0f, PhysicsMath.ComputeFalloff(curve, 4.01f, 1f, 4f), 0f, curve.ToString());
            }
        }

        [Test]
        public void Falloff_Linear_IsHalfAtMidRange()
        {
            Assert.AreEqual(0.5f, PhysicsMath.ComputeFalloff(PhysicsTriggerFalloffCurve.Linear, 2.5f, 1f, 4f),
                0.0001f);
        }

        [Test]
        public void Falloff_InverseSquare_FollowsOneOverDistanceSquared()
        {
            // Normalized to 1 at the start radius: at 2x the start radius the attenuation is 1/4.
            Assert.AreEqual(0.25f, PhysicsMath.ComputeFalloff(PhysicsTriggerFalloffCurve.InverseSquare, 2f, 1f, 10f),
                0.0001f);
            Assert.AreEqual(1f / 9f, PhysicsMath.ComputeFalloff(PhysicsTriggerFalloffCurve.InverseSquare, 3f, 1f, 10f),
                0.0001f);
        }

        [Test]
        public void Falloff_Step_IsFullUpToEndRadius()
        {
            Assert.AreEqual(1f, PhysicsMath.ComputeFalloff(PhysicsTriggerFalloffCurve.Step, 3.99f, 1f, 4f));
        }

        [Test]
        public void Falloff_IsContinuousAtStartRadius()
        {
            foreach (var curve in new[]
                     {
                         PhysicsTriggerFalloffCurve.Linear,
                         PhysicsTriggerFalloffCurve.InverseSquare,
                         PhysicsTriggerFalloffCurve.Step
                     })
            {
                var justInside = PhysicsMath.ComputeFalloff(curve, 1f, 1f, 4f);
                var justOutside = PhysicsMath.ComputeFalloff(curve, 1.0001f, 1f, 4f);
                Assert.AreEqual(justInside, justOutside, 0.001f, curve.ToString());
            }
        }
    }

    public class TeleportReferenceFrameTests
    {
        private static readonly float3 Self = new(0f, 0f, 10f);
        private static readonly float3 Target = float3.zero;

        private static quaternion Resolve(TeleportReferenceFrame frame, quaternion targetRotation)
        {
            TeleportMath.ResolveReferenceRotation(Self, Target, targetRotation, frame, out var rotation);
            return rotation;
        }

        [Test]
        public void TargetToSelf_ForwardPointsFromTargetTowardSelf()
        {
            var forward = math.mul(Resolve(TeleportReferenceFrame.TargetToSelf, quaternion.identity), math.forward());
            Assert.AreEqual(1f, math.dot(forward, math.normalize(Self - Target)), 0.0001f);
        }

        [Test]
        public void SelfToTarget_ForwardPointsFromSelfTowardTarget()
        {
            var forward = math.mul(Resolve(TeleportReferenceFrame.SelfToTarget, quaternion.identity), math.forward());
            Assert.AreEqual(1f, math.dot(forward, math.normalize(Target - Self)), 0.0001f);
        }

        [Test]
        public void TargetForward_UsesAzimuthTargetRotation()
        {
            var targetRotation = quaternion.RotateY(math.PI * 0.5f);
            var rotation = Resolve(TeleportReferenceFrame.TargetForward, targetRotation);

            var expected = math.mul(targetRotation, math.forward());
            var actual = math.mul(rotation, math.forward());
            Assert.AreEqual(1f, math.dot(expected, actual), 0.0001f);
        }

        [Test]
        public void WorldForward_IsIdentity()
        {
            var rotation = Resolve(TeleportReferenceFrame.WorldForward, quaternion.RotateY(1f));
            var forward = math.mul(rotation, math.forward());
            Assert.AreEqual(1f, math.dot(forward, math.forward()), 0.0001f);
        }

        [Test]
        public void DirectionalFrames_DegenerateSeparation_FallBackToTargetRotation()
        {
            var targetRotation = quaternion.RotateY(math.PI * 0.25f);
            TeleportMath.ResolveReferenceRotation(Target, Target, targetRotation,
                TeleportReferenceFrame.SelfToTarget, out var rotation);

            var expected = math.mul(targetRotation, math.forward());
            var actual = math.mul(rotation, math.forward());
            Assert.AreEqual(1f, math.dot(expected, actual), 0.0001f);
        }
    }
}
