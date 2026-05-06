using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Smear;
using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    [TestFixture]
    public class EnumValueTests
    {
        [Test]
        public void PhysicsVelocityMode_Values()
        {
            Assert.AreEqual(0, (int)PhysicsVelocityMode.SetContinuous);
            Assert.AreEqual(1, (int)PhysicsVelocityMode.SetInstant);
            Assert.AreEqual(2, (int)PhysicsVelocityMode.AddContinuous);
            Assert.AreEqual(3, (int)PhysicsVelocityMode.AddInstant);
        }

        [Test]
        public void PhysicsForceMode_Values()
        {
            Assert.AreEqual(0, (int)PhysicsForceMode.Continuous);
            Assert.AreEqual(1, (int)PhysicsForceMode.Impulse);
        }

        [Test]
        public void PidLinearTargetMode_Values()
        {
            Assert.AreEqual(0, (int)PidLinearTargetMode.TargetLocal);
            Assert.AreEqual(1, (int)PidLinearTargetMode.InitialLocal);
            Assert.AreEqual(2, (int)PidLinearTargetMode.LineOfSight);
            Assert.AreEqual(3, (int)PidLinearTargetMode.World);
            Assert.AreEqual(4, (int)PidLinearTargetMode.FleeFromTarget);
        }

        [Test]
        public void PidAngularTargetMode_Values()
        {
            Assert.AreEqual(0, (int)PidAngularTargetMode.MatchTarget);
            Assert.AreEqual(1, (int)PidAngularTargetMode.LookAtTarget);
            Assert.AreEqual(2, (int)PidAngularTargetMode.World);
            Assert.AreEqual(3, (int)PidAngularTargetMode.FleeFromTarget);
            Assert.AreEqual(4, (int)PidAngularTargetMode.MatchTargetOpposite);
        }

        [Test]
        public void PhysicsTriggerPositionMode_Values()
        {
            Assert.AreEqual(0, (int)PhysicsTriggerPositionMode.MatchSelf);
            Assert.AreEqual(1, (int)PhysicsTriggerPositionMode.MatchCollidedEntity);
            Assert.AreEqual(2, (int)PhysicsTriggerPositionMode.MatchContactPoint);
        }

        [Test]
        public void PhysicsTriggerRotationMode_Values()
        {
            Assert.AreEqual(0, (int)PhysicsTriggerRotationMode.MatchSelf);
            Assert.AreEqual(1, (int)PhysicsTriggerRotationMode.MatchCollidedEntity);
            Assert.AreEqual(2, (int)PhysicsTriggerRotationMode.AlignToContactNormal);
            Assert.AreEqual(3, (int)PhysicsTriggerRotationMode.Identity);
        }

        [Test]
        public void PhysicsTriggerTargetMode_Values()
        {
            Assert.AreEqual(0, (int)PhysicsTriggerTargetMode.Self);
            Assert.AreEqual(1, (int)PhysicsTriggerTargetMode.CollidedEntity);
            Assert.AreEqual(2, (int)PhysicsTriggerTargetMode.ReactionOwner);
            Assert.AreEqual(3, (int)PhysicsTriggerTargetMode.ReactionSource);
            Assert.AreEqual(4, (int)PhysicsTriggerTargetMode.ReactionTarget);
        }
    }

    [TestFixture]
    public class PhysicsDragDataStructTests
    {
        [Test]
        public void Default_ZeroFields()
        {
            var d = new PhysicsDragData();
            Assert.AreEqual(0f, d.Linear);
            Assert.AreEqual(0f, d.Angular);
        }

        [Test]
        public void Fields_SetCorrectly()
        {
            var d = new PhysicsDragData { Linear = 3.5f, Angular = 7.2f };
            Assert.AreEqual(3.5f, d.Linear);
            Assert.AreEqual(7.2f, d.Angular);
        }
    }

    [TestFixture]
    public class PhysicsForceDataStructTests
    {
        [Test]
        public void Default_ZeroFields()
        {
            var d = new PhysicsForceData();
            Assert.AreEqual(PhysicsForceMode.Continuous, d.Mode);
            Assert.AreEqual(float3.zero, d.Linear);
            Assert.AreEqual(float3.zero, d.Angular);
            Assert.AreEqual(Target.None, d.Space);
        }

        [Test]
        public void Fields_SetCorrectly()
        {
            var d = new PhysicsForceData
            {
                Mode = PhysicsForceMode.Impulse,
                Linear = new float3(1, 2, 3),
                Angular = new float3(4, 5, 6),
                Space = Target.Self
            };
            Assert.AreEqual(PhysicsForceMode.Impulse, d.Mode);
            Assert.AreEqual(new float3(1, 2, 3), d.Linear);
            Assert.AreEqual(new float3(4, 5, 6), d.Angular);
            Assert.AreEqual(Target.Self, d.Space);
        }
    }

    [TestFixture]
    public class PhysicsVelocityDataStructTests
    {
        [Test]
        public void Default_ZeroFields()
        {
            var d = new PhysicsVelocityData();
            Assert.AreEqual(PhysicsVelocityMode.SetContinuous, d.Mode);
            Assert.AreEqual(float3.zero, d.Linear);
            Assert.AreEqual(float3.zero, d.Angular);
            Assert.AreEqual(Target.None, d.Space);
        }

        [Test]
        public void Fields_SetCorrectly()
        {
            var d = new PhysicsVelocityData
            {
                Mode = PhysicsVelocityMode.AddInstant,
                Linear = new float3(10, 20, 30),
                Angular = new float3(1, 2, 3),
                Space = Target.Source
            };
            Assert.AreEqual(PhysicsVelocityMode.AddInstant, d.Mode);
            Assert.AreEqual(new float3(10, 20, 30), d.Linear);
            Assert.AreEqual(new float3(1, 2, 3), d.Angular);
            Assert.AreEqual(Target.Source, d.Space);
        }
    }

    [TestFixture]
    public class PhysicsLinearPIDDataStructTests
    {
        [Test]
        public void Fields_SetCorrectly()
        {
            var d = new PhysicsLinearPIDData
            {
                Tuning = new PidTuning { Proportional = new float3(1f), MaxOutput = 50f },
                TrackingTarget = Target.Self,
                TargetMode = PidLinearTargetMode.World,
                TargetOffset = new float3(1, 2, 3),
                Strength = 2.5f
            };
            Assert.AreEqual(new float3(1f), d.Tuning.Proportional);
            Assert.AreEqual(Target.Self, d.TrackingTarget);
            Assert.AreEqual(PidLinearTargetMode.World, d.TargetMode);
            Assert.AreEqual(new float3(1, 2, 3), d.TargetOffset);
            Assert.AreEqual(2.5f, d.Strength);
        }
    }

    [TestFixture]
    public class PhysicsAngularPIDDataStructTests
    {
        [Test]
        public void Fields_SetCorrectly()
        {
            var d = new PhysicsAngularPIDData
            {
                Tuning = new PidTuning { Proportional = new float3(1f), MaxOutput = 50f },
                TrackingTarget = Target.Source,
                TargetMode = PidAngularTargetMode.LookAtTarget,
                TargetRotation = quaternion.RotateY(1.5f),
                Strength = 3.0f
            };
            Assert.AreEqual(new float3(1f), d.Tuning.Proportional);
            Assert.AreEqual(Target.Source, d.TrackingTarget);
            Assert.AreEqual(PidAngularTargetMode.LookAtTarget, d.TargetMode);
            Assert.AreEqual(3.0f, d.Strength);
        }
    }

    [TestFixture]
    public class PhysicsLinearPIDMixerTests
    {
        private static PhysicsLinearPIDData MakeLPID(PidLinearTargetMode mode, float3 offset, float strength)
        {
            return new PhysicsLinearPIDData
            {
                Tuning = new PidTuning { Proportional = new float3(strength), MaxOutput = 100f },
                TrackingTarget = Target.Self,
                TargetMode = mode,
                TargetOffset = offset,
                Strength = strength
            };
        }

        [Test]
        public void Lerp_S0_ReturnsA()
        {
            var a = MakeLPID(PidLinearTargetMode.World, new float3(1, 0, 0), 5f);
            var b = MakeLPID(PidLinearTargetMode.FleeFromTarget, new float3(0, 1, 0), 10f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.TargetOffset, result.TargetOffset);
            Assert.AreEqual(a.Strength, result.Strength, 0.001f);
            Assert.AreEqual(a.TargetMode, result.TargetMode);
        }

        [Test]
        public void Lerp_S1_ReturnsB()
        {
            var a = MakeLPID(PidLinearTargetMode.World, new float3(1, 0, 0), 5f);
            var b = MakeLPID(PidLinearTargetMode.FleeFromTarget, new float3(0, 1, 0), 10f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.TargetOffset, result.TargetOffset);
            Assert.AreEqual(b.Strength, result.Strength, 0.001f);
        }

        [Test]
        public void Lerp_S05_Interpolates()
        {
            var a = MakeLPID(PidLinearTargetMode.World, new float3(0, 0, 0), 0f);
            var b = MakeLPID(PidLinearTargetMode.World, new float3(10, 20, 30), 20f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(new float3(5, 10, 15), result.TargetOffset);
            Assert.AreEqual(10f, result.Strength, 0.001f);
        }

        [Test]
        public void Lerp_S049_TakesAEnums()
        {
            var a = MakeLPID(PidLinearTargetMode.World, float3.zero, 1f);
            var b = MakeLPID(PidLinearTargetMode.LineOfSight, float3.zero, 1f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Lerp(a, b, 0.49f);

            Assert.AreEqual(a.TargetMode, result.TargetMode);
            Assert.AreEqual(a.TrackingTarget, result.TrackingTarget);
        }

        [Test]
        public void Lerp_S05_TakesBEnums()
        {
            var a = MakeLPID(PidLinearTargetMode.World, float3.zero, 1f);
            var b = MakeLPID(PidLinearTargetMode.LineOfSight, float3.zero, 1f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(b.TargetMode, result.TargetMode);
        }

        [Test]
        public void Add_SumsOffsetAndStrength_TakesAEnums()
        {
            var a = MakeLPID(PidLinearTargetMode.World, new float3(1, 2, 3), 5f);
            var b = MakeLPID(PidLinearTargetMode.FleeFromTarget, new float3(10, 20, 30), 15f);
            var mixer = new PhysicsLinearPIDMixer();

            var result = mixer.Add(a, b);

            Assert.AreEqual(new float3(11, 22, 33), result.TargetOffset);
            Assert.AreEqual(20f, result.Strength, 0.001f);
            Assert.AreEqual(a.TargetMode, result.TargetMode);
            Assert.AreEqual(a.TrackingTarget, result.TrackingTarget);
        }
    }

    [TestFixture]
    public class PhysicsAngularPIDMixerTests
    {
        private static PhysicsAngularPIDData MakeAPID(PidAngularTargetMode mode, quaternion rot, float strength)
        {
            return new PhysicsAngularPIDData
            {
                Tuning = new PidTuning { Proportional = new float3(strength), MaxOutput = 100f },
                TrackingTarget = Target.Self,
                TargetMode = mode,
                TargetRotation = rot,
                Strength = strength
            };
        }

        [Test]
        public void Lerp_S0_ReturnsA()
        {
            var a = MakeAPID(PidAngularTargetMode.MatchTarget, quaternion.identity, 5f);
            var b = MakeAPID(PidAngularTargetMode.World, quaternion.RotateY(math.PI), 10f);
            var mixer = new PhysicsAngularPIDMixer();

            var result = mixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.TargetMode, result.TargetMode);
            Assert.AreEqual(a.Strength, result.Strength, 0.001f);
        }

        [Test]
        public void Lerp_S1_ReturnsB()
        {
            var a = MakeAPID(PidAngularTargetMode.MatchTarget, quaternion.identity, 5f);
            var b = MakeAPID(PidAngularTargetMode.World, quaternion.RotateY(math.PI), 10f);
            var mixer = new PhysicsAngularPIDMixer();

            var result = mixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.Strength, result.Strength, 0.001f);
        }

        [Test]
        public void Lerp_S05_InterpolatesStrength()
        {
            var a = MakeAPID(PidAngularTargetMode.World, quaternion.identity, 0f);
            var b = MakeAPID(PidAngularTargetMode.World, quaternion.identity, 20f);
            var mixer = new PhysicsAngularPIDMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(10f, result.Strength, 0.001f);
        }

        [Test]
        public void Add_SumsStrength_TakesAEnums()
        {
            var a = MakeAPID(PidAngularTargetMode.MatchTarget, quaternion.identity, 5f);
            var b = MakeAPID(PidAngularTargetMode.LookAtTarget, quaternion.RotateY(1f), 15f);
            var mixer = new PhysicsAngularPIDMixer();

            var result = mixer.Add(a, b);

            Assert.AreEqual(20f, result.Strength, 0.001f);
            Assert.AreEqual(a.TargetMode, result.TargetMode);
            Assert.AreEqual(a.TrackingTarget, result.TrackingTarget);
        }
    }

    [TestFixture]
    public class SmearVelocityStructTests
    {
        [Test]
        public void Default_ZeroValue()
        {
            var s = new SmearVelocity();
            Assert.AreEqual(float4.zero, s.Value);
        }

        [Test]
        public void Field_SetCorrectly()
        {
            var s = new SmearVelocity { Value = new float4(1, 2, 3, 4) };
            Assert.AreEqual(new float4(1, 2, 3, 4), s.Value);
        }
    }

    [TestFixture]
    public class StatefulEventStateConfigStructTests
    {
        [Test]
        public void Default_ZeroValue()
        {
            var c = new StatefulEventStateConfig();
            Assert.AreEqual(default(StatefulEventState), c.Value);
        }
    }

    [TestFixture]
    public class PhysicsForceStateStructTests
    {
        [Test]
        public void Default_NotFired()
        {
            var s = new PhysicsForceState();
            Assert.IsFalse(s.Fired);
        }

        [Test]
        public void Field_SetCorrectly()
        {
            var s = new PhysicsForceState { Fired = true };
            Assert.IsTrue(s.Fired);
        }
    }

    [TestFixture]
    public class PhysicsVelocityStateStructTests
    {
        [Test]
        public void Default_NotFired()
        {
            var s = new PhysicsVelocityState();
            Assert.IsFalse(s.Fired);
        }
    }
}