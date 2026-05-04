using BovineLabs.Reaction.Data.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    [TestFixture]
    public class PhysicsVelocityMixerTests
    {
        private static PhysicsVelocityData MakeVD(PhysicsVelocityMode mode, float3 lin, float3 ang, Target space)
        {
            return new PhysicsVelocityData { Mode = mode, Linear = lin, Angular = ang, Space = space };
        }

        [Test]
        public void Lerp_S0_ReturnsAValues()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeVD(PhysicsVelocityMode.AddInstant, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.Linear, result.Linear);
            Assert.AreEqual(a.Angular, result.Angular);
            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Lerp_S1_ReturnsBValues()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeVD(PhysicsVelocityMode.AddInstant, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.Linear, result.Linear);
            Assert.AreEqual(b.Angular, result.Angular);
            Assert.AreEqual(b.Mode, result.Mode);
            Assert.AreEqual(b.Space, result.Space);
        }

        [Test]
        public void Lerp_S049_TakesAMode()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, float3.zero, float3.zero, Target.Self);
            var b = MakeVD(PhysicsVelocityMode.AddInstant, float3.zero, float3.zero, Target.Source);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Lerp(a, b, 0.49f);

            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Lerp_S05_TakesBMode()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, float3.zero, float3.zero, Target.Self);
            var b = MakeVD(PhysicsVelocityMode.AddInstant, float3.zero, float3.zero, Target.Source);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(b.Mode, result.Mode);
            Assert.AreEqual(b.Space, result.Space);
        }

        [Test]
        public void Lerp_S05_InterpolatesLinearAngular()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, new float3(0, 0, 0), new float3(0, 0, 0), Target.Self);
            var b = MakeVD(PhysicsVelocityMode.SetContinuous, new float3(10, 20, 30), new float3(40, 50, 60), Target.Self);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(new float3(5, 10, 15), result.Linear);
            Assert.AreEqual(new float3(20, 25, 30), result.Angular);
        }

        [Test]
        public void Add_SumsLinearAngular_TakesAMode()
        {
            var a = MakeVD(PhysicsVelocityMode.SetContinuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeVD(PhysicsVelocityMode.AddInstant, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Add(a, b);

            Assert.AreEqual(new float3(11, 22, 33), result.Linear);
            Assert.AreEqual(new float3(44, 55, 66), result.Angular);
            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Add_WithZero_NoChange()
        {
            var a = MakeVD(PhysicsVelocityMode.SetInstant, new float3(7, 8, 9), new float3(10, 11, 12), Target.Source);
            var z = MakeVD(PhysicsVelocityMode.AddContinuous, float3.zero, float3.zero, Target.Self);
            var mixer = new PhysicsVelocityMixer();

            var result = mixer.Add(a, z);

            Assert.AreEqual(a.Linear, result.Linear);
            Assert.AreEqual(a.Angular, result.Angular);
        }
    }

    [TestFixture]
    public class PhysicsDragMixerTests
    {
        [Test]
        public void Lerp_S0_ReturnsA()
        {
            var a = new PhysicsDragData { Linear = 5f, Angular = 10f };
            var b = new PhysicsDragData { Linear = 20f, Angular = 40f };
            var mixer = new PhysicsDragMixer();

            var result = mixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.Linear, result.Linear, 0.001f);
            Assert.AreEqual(a.Angular, result.Angular, 0.001f);
        }

        [Test]
        public void Lerp_S1_ReturnsB()
        {
            var a = new PhysicsDragData { Linear = 5f, Angular = 10f };
            var b = new PhysicsDragData { Linear = 20f, Angular = 40f };
            var mixer = new PhysicsDragMixer();

            var result = mixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.Linear, result.Linear, 0.001f);
            Assert.AreEqual(b.Angular, result.Angular, 0.001f);
        }

        [Test]
        public void Lerp_S05_Interpolates()
        {
            var a = new PhysicsDragData { Linear = 0f, Angular = 0f };
            var b = new PhysicsDragData { Linear = 10f, Angular = 20f };
            var mixer = new PhysicsDragMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(5f, result.Linear, 0.001f);
            Assert.AreEqual(10f, result.Angular, 0.001f);
        }

        [Test]
        public void Add_SumsBoth()
        {
            var a = new PhysicsDragData { Linear = 3f, Angular = 7f };
            var b = new PhysicsDragData { Linear = 2f, Angular = 3f };
            var mixer = new PhysicsDragMixer();

            var result = mixer.Add(a, b);

            Assert.AreEqual(5f, result.Linear, 0.001f);
            Assert.AreEqual(10f, result.Angular, 0.001f);
        }

        [Test]
        public void Add_WithZero_NoChange()
        {
            var a = new PhysicsDragData { Linear = 5f, Angular = 15f };
            var z = new PhysicsDragData();
            var mixer = new PhysicsDragMixer();

            var result = mixer.Add(a, z);

            Assert.AreEqual(a.Linear, result.Linear, 0.001f);
            Assert.AreEqual(a.Angular, result.Angular, 0.001f);
        }
    }

    [TestFixture]
    public class PhysicsForceMixerTests
    {
        private static PhysicsForceData MakeFD(PhysicsForceMode mode, float3 lin, float3 ang, Target space)
        {
            return new PhysicsForceData { Mode = mode, Linear = lin, Angular = ang, Space = space };
        }

        [Test]
        public void Lerp_S0_ReturnsA()
        {
            var a = MakeFD(PhysicsForceMode.Continuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeFD(PhysicsForceMode.Impulse, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.Linear, result.Linear);
            Assert.AreEqual(a.Angular, result.Angular);
            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Lerp_S1_ReturnsB()
        {
            var a = MakeFD(PhysicsForceMode.Continuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeFD(PhysicsForceMode.Impulse, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.Linear, result.Linear);
            Assert.AreEqual(b.Angular, result.Angular);
            Assert.AreEqual(b.Mode, result.Mode);
            Assert.AreEqual(b.Space, result.Space);
        }

        [Test]
        public void Lerp_S049_TakesAModeAndSpace()
        {
            var a = MakeFD(PhysicsForceMode.Continuous, float3.zero, float3.zero, Target.Self);
            var b = MakeFD(PhysicsForceMode.Impulse, float3.zero, float3.zero, Target.Source);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Lerp(a, b, 0.49f);

            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Lerp_S05_TakesBModeAndSpace()
        {
            var a = MakeFD(PhysicsForceMode.Continuous, float3.zero, float3.zero, Target.Self);
            var b = MakeFD(PhysicsForceMode.Impulse, float3.zero, float3.zero, Target.Source);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(b.Mode, result.Mode);
            Assert.AreEqual(b.Space, result.Space);
        }

        [Test]
        public void Add_SumsLinearAngular_TakesAModeAndSpace()
        {
            var a = MakeFD(PhysicsForceMode.Continuous, new float3(1, 2, 3), new float3(4, 5, 6), Target.Self);
            var b = MakeFD(PhysicsForceMode.Impulse, new float3(10, 20, 30), new float3(40, 50, 60), Target.Source);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Add(a, b);

            Assert.AreEqual(new float3(11, 22, 33), result.Linear);
            Assert.AreEqual(new float3(44, 55, 66), result.Angular);
            Assert.AreEqual(a.Mode, result.Mode);
            Assert.AreEqual(a.Space, result.Space);
        }

        [Test]
        public void Add_WithZero_NoChange()
        {
            var a = MakeFD(PhysicsForceMode.Impulse, new float3(7, 8, 9), new float3(10, 11, 12), Target.Source);
            var z = MakeFD(PhysicsForceMode.Continuous, float3.zero, float3.zero, Target.Self);
            var mixer = new PhysicsForceMixer();

            var result = mixer.Add(a, z);

            Assert.AreEqual(a.Linear, result.Linear);
            Assert.AreEqual(a.Angular, result.Angular);
        }
    }
}
