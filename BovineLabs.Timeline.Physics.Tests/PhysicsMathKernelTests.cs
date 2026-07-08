using BovineLabs.Nerve.PhysicsStates;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using BovineLabs.Timeline.Physics.Infrastructure;
using BovineLabs.Timeline.Physics.Teleports;
using NUnit.Framework;
using Unity.Entities;
using Unity.IntegerTime;
using Unity.Mathematics;
using Unity.Transforms;

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
        public void Reflect_HeadOnIntoWall_ReversesDirection()
        {
            var result = PhysicsMath.Reflect(new float3(1f, 0f, 0f), new float3(-1f, 0f, 0f));

            Assert.AreEqual(-1f, result.x, 1e-5f);
            Assert.AreEqual(0f, result.y, 1e-5f);
            Assert.AreEqual(0f, result.z, 1e-5f);
        }

        [Test]
        public void Reflect_OffFloor_FlipsVerticalComponentOnly()
        {
            var result = PhysicsMath.Reflect(new float3(1f, -1f, 0f), new float3(0f, 1f, 0f));

            Assert.AreEqual(1f, result.x, 1e-5f);
            Assert.AreEqual(1f, result.y, 1e-5f);
            Assert.AreEqual(0f, result.z, 1e-5f);
        }

        [Test]
        public void Reflect_PreservesMagnitude_ForUnitNormal()
        {
            var direction = new float3(0.3f, -0.8f, 0.5f);
            var normal = math.normalize(new float3(0.2f, 1f, -0.4f));

            var result = PhysicsMath.Reflect(direction, normal);

            Assert.AreEqual(math.length(direction), math.length(result), 1e-5f);
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

    public class StatefulEventMatchingTests
    {
        [Test]
        public void Stay_MatchesExit_OnForwardLastFrame()
        {
            var timer = new TimerData
            {
                Time = DiscreteTime.FromTicks(10),
                DeltaTime = DiscreteTime.FromTicks(1)
            };
            var transform = new TimeTransform
            {
                Start = DiscreteTime.Zero,
                End = DiscreteTime.FromTicks(10),
                Scale = 1
            };

            var isLastFrame = StatefulEventMatching.IsClipLastFrame(in timer, in transform);

            Assert.IsTrue(isLastFrame);
            Assert.IsTrue(StatefulEventMatching.Matches(
                StatefulEventState.Stay, StatefulEventState.Exit, false, isLastFrame));
        }

        [Test]
        public void Stay_MatchesExit_OnReverseLastFrame()
        {
            var timer = new TimerData
            {
                Time = DiscreteTime.Zero,
                DeltaTime = DiscreteTime.FromTicks(-1)
            };
            var transform = new TimeTransform
            {
                Start = DiscreteTime.Zero,
                End = DiscreteTime.FromTicks(10),
                Scale = 1
            };

            Assert.IsTrue(StatefulEventMatching.IsClipLastFrame(in timer, in transform));
        }

        [Test]
        public void Stay_DoesNotMatchExit_BeforeLastFrame()
        {
            var timer = new TimerData
            {
                Time = DiscreteTime.FromTicks(9),
                DeltaTime = DiscreteTime.FromTicks(1)
            };
            var transform = new TimeTransform
            {
                Start = DiscreteTime.Zero,
                End = DiscreteTime.FromTicks(10),
                Scale = 1
            };

            Assert.IsFalse(StatefulEventMatching.IsClipLastFrame(in timer, in transform));
        }

        [Test]
        public void PostExtrapolation_DoesNotRepeatLastFrame()
        {
            var timer = new TimerData
            {
                Time = DiscreteTime.FromTicks(11),
                DeltaTime = DiscreteTime.FromTicks(1)
            };
            var transform = new TimeTransform
            {
                Start = DiscreteTime.Zero,
                End = DiscreteTime.FromTicks(10),
                Scale = 1
            };

            Assert.IsFalse(StatefulEventMatching.IsClipLastFrame(in timer, in transform));
        }

        // ------------------------------------------------------------------------------------------------
        // Crossing-aware activation (PhysicsClipGate kernel). The point-sample core uses (IsLocalTimeBounded)
        // misses a clip window that one low-FPS timeline step jumps clean over; the crossing test must not.
        // ------------------------------------------------------------------------------------------------

        private static readonly TimeTransform Window4To6 = new()
        {
            Start = DiscreteTime.FromTicks(4),
            End = DiscreteTime.FromTicks(6),
            Scale = 1,
        };

        [Test]
        public void Crossing_WholeWindowSteppedOver_IsActive_WherePointSampleMisses()
        {
            // The verdict's repro: step time 1 -> 11 across clip [4,6].
            var timer = new TimerData { Time = DiscreteTime.FromTicks(11), DeltaTime = DiscreteTime.FromTicks(10) };

            // Point sample (what core does) sees only t=11 -> local 7, length 2 -> out of bounds -> inactive.
            Assert.IsFalse(Window4To6.IsLocalTimeBounded(Window4To6.ToLocalTimeUnbound(timer.Time)));

            // Crossing-aware test recovers it.
            Assert.IsTrue(StatefulEventMatching.IsClipActiveCrossing(in timer, in Window4To6));
        }

        [Test]
        public void Crossing_InsideWindow_IsActive()
        {
            var timer = new TimerData { Time = DiscreteTime.FromTicks(5), DeltaTime = DiscreteTime.FromTicks(1) };
            Assert.IsTrue(StatefulEventMatching.IsClipActiveCrossing(in timer, in Window4To6));
        }

        [Test]
        public void Crossing_PausedInsideWindow_IsActive()
        {
            var timer = new TimerData { Time = DiscreteTime.FromTicks(5), DeltaTime = DiscreteTime.Zero };
            Assert.IsTrue(StatefulEventMatching.IsClipActiveCrossing(in timer, in Window4To6));
        }

        [Test]
        public void Crossing_EntirelyBeforeWindow_IsInactive()
        {
            var timer = new TimerData { Time = DiscreteTime.FromTicks(3), DeltaTime = DiscreteTime.FromTicks(1) };
            Assert.IsFalse(StatefulEventMatching.IsClipActiveCrossing(in timer, in Window4To6));
        }

        [Test]
        public void Crossing_EntirelyAfterWindow_IsInactive()
        {
            var timer = new TimerData { Time = DiscreteTime.FromTicks(8), DeltaTime = DiscreteTime.FromTicks(1) };
            Assert.IsFalse(StatefulEventMatching.IsClipActiveCrossing(in timer, in Window4To6));
        }

        [Test]
        public void Crossing_ZeroDurationClip_IsInactive_NotSpuriouslyLatched()
        {
            // Start == End -> length 0. The interval crosses the point but a zero-duration clip is not a window;
            // it must not crossing-activate (core point-sampling handles the instant).
            var zero = new TimeTransform { Start = DiscreteTime.FromTicks(5), End = DiscreteTime.FromTicks(5), Scale = 1 };
            var timer = new TimerData { Time = DiscreteTime.FromTicks(7), DeltaTime = DiscreteTime.FromTicks(4) };
            Assert.IsFalse(StatefulEventMatching.IsClipActiveCrossing(in timer, in zero));
        }

        [Test]
        public void Crossing_ScaleZeroClip_IsInactive()
        {
            // Scale 0 collapses local time to a constant -> length 0; must not report perpetually active.
            var frozen = new TimeTransform { Start = DiscreteTime.FromTicks(4), End = DiscreteTime.FromTicks(6), Scale = 0 };
            var timer = new TimerData { Time = DiscreteTime.FromTicks(5), DeltaTime = DiscreteTime.FromTicks(1) };
            Assert.IsFalse(StatefulEventMatching.IsClipActiveCrossing(in timer, in frozen));
        }
    }

    public class TeleportPlacementTests
    {
        [Test]
        public void Unparented_PreservesLocalScale()
        {
            var frame = CreateFrame();
            var data = new PhysicsTeleportData { FacingMode = TeleportFacingMode.PreserveCurrent };

            var transform = TeleportPlacement.ComputeLocalTransform(
                in frame, in data, new float3(3f, 4f, 5f), 2.5f, false, default);

            Assert.AreEqual(2.5f, transform.Scale, 0.0001f);
        }

        [Test]
        public void Parented_PreservesLocalScale()
        {
            var frame = CreateFrame();
            var data = new PhysicsTeleportData { FacingMode = TeleportFacingMode.PreserveCurrent };
            var parent = new LocalToWorld { Value = float4x4.Translate(new float3(1f, 0f, 0f)) };

            var transform = TeleportPlacement.ComputeLocalTransform(
                in frame, in data, new float3(3f, 4f, 5f), 2.5f, true, in parent);

            Assert.AreEqual(2.5f, transform.Scale, 0.0001f);
        }

        [Test]
        public void Parented_UnderScaledParent_LandsAtCorrectWorldPosition()
        {
            var frame = CreateFrame();
            var data = new PhysicsTeleportData { FacingMode = TeleportFacingMode.PreserveCurrent };
            var parent = new LocalToWorld
            {
                Value = float4x4.TRS(new float3(1f, 2f, 3f), quaternion.identity, new float3(2f)),
            };
            var landing = new float3(3f, 4f, 5f);

            var transform = TeleportPlacement.ComputeLocalTransform(
                in frame, in data, landing, 1f, true, in parent);

            // A uniform-scale-2 parent must not multiply the offset: reconstructing the world position from
            // the parent transform must reproduce the requested landing, not 2x it.
            var world = math.transform(parent.Value, transform.Position);
            Assert.AreEqual(landing.x, world.x, 0.0001f);
            Assert.AreEqual(landing.y, world.y, 0.0001f);
            Assert.AreEqual(landing.z, world.z, 0.0001f);
        }

        private static TeleportFrame CreateFrame()
        {
            return new TeleportFrame(
                true,
                new Entity { Index = 1, Version = 1 },
                float3.zero,
                quaternion.identity,
                Entity.Null,
                float3.zero,
                float3.zero,
                quaternion.identity,
                float3.zero,
                quaternion.identity,
                quaternion.identity);
        }
    }
}