using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    [TestFixture]
    public class PidTuningTests
    {
        [Test]
        public void DefaultConstructor_AllFieldsZero()
        {
            var t = new PidTuning();
            Assert.AreEqual(float3.zero, t.Proportional);
            Assert.AreEqual(float3.zero, t.Derivative);
            Assert.AreEqual(float3.zero, t.Integral);
            Assert.AreEqual(0f, t.MaxOutput);
        }

        [Test]
        public void Fields_SetCorrectly()
        {
            var t = new PidTuning
            {
                Proportional = new float3(1, 2, 3),
                Derivative = new float3(4, 5, 6),
                Integral = new float3(7, 8, 9),
                MaxOutput = 42f
            };
            Assert.AreEqual(new float3(1, 2, 3), t.Proportional);
            Assert.AreEqual(new float3(4, 5, 6), t.Derivative);
            Assert.AreEqual(new float3(7, 8, 9), t.Integral);
            Assert.AreEqual(42f, t.MaxOutput);
        }
    }

    [TestFixture]
    public class PidStateDataTests
    {
        [Test]
        public void DefaultConstructor_AllZero_Uninitialized()
        {
            var s = new PidStateData();
            Assert.AreEqual(float3.zero, s.IntegralAccumulator);
            Assert.AreEqual(float3.zero, s.PreviousError);
            Assert.AreEqual(float3.zero, s.CapturedTargetPosition);
            Assert.IsFalse(s.IsInitialized);
        }

        [Test]
        public void Fields_SetCorrectly()
        {
            var s = new PidStateData
            {
                IntegralAccumulator = new float3(1, 2, 3),
                PreviousError = new float3(4, 5, 6),
                CapturedTargetPosition = new float3(7, 8, 9),
                IsInitialized = true
            };
            Assert.AreEqual(new float3(1, 2, 3), s.IntegralAccumulator);
            Assert.AreEqual(new float3(4, 5, 6), s.PreviousError);
            Assert.AreEqual(new float3(7, 8, 9), s.CapturedTargetPosition);
            Assert.IsTrue(s.IsInitialized);
        }
    }

    [TestFixture]
    public class PidMixerTests
    {
        private static PidTuning MakeA()
        {
            return new PidTuning
            {
                Proportional = new float3(1, 0, 0),
                Derivative = new float3(2, 0, 0),
                Integral = new float3(3, 0, 0),
                MaxOutput = 10f
            };
        }

        private static PidTuning MakeB()
        {
            return new PidTuning
            {
                Proportional = new float3(0, 1, 0),
                Derivative = new float3(0, 2, 0),
                Integral = new float3(0, 3, 0),
                MaxOutput = 20f
            };
        }

        [Test]
        public void Lerp_S0_ReturnsA()
        {
            var a = MakeA();
            var b = MakeB();
            var result = PidMixer.Lerp(a, b, 0f);

            Assert.AreEqual(a.Proportional, result.Proportional);
            Assert.AreEqual(a.Derivative, result.Derivative);
            Assert.AreEqual(a.Integral, result.Integral);
            Assert.AreEqual(a.MaxOutput, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Lerp_S1_ReturnsB()
        {
            var a = MakeA();
            var b = MakeB();
            var result = PidMixer.Lerp(a, b, 1f);

            Assert.AreEqual(b.Proportional, result.Proportional);
            Assert.AreEqual(b.Derivative, result.Derivative);
            Assert.AreEqual(b.Integral, result.Integral);
            Assert.AreEqual(b.MaxOutput, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Lerp_S05_Interpolates()
        {
            var a = MakeA();
            var b = MakeB();
            var result = PidMixer.Lerp(a, b, 0.5f);

            Assert.AreEqual(math.lerp(a.Proportional, b.Proportional, 0.5f), result.Proportional);
            Assert.AreEqual(math.lerp(a.Derivative, b.Derivative, 0.5f), result.Derivative);
            Assert.AreEqual(math.lerp(a.Integral, b.Integral, 0.5f), result.Integral);
            Assert.AreEqual(15f, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Lerp_SameInputs_ReturnsSame()
        {
            var a = MakeA();
            var result = PidMixer.Lerp(a, a, 0.37f);

            Assert.AreEqual(a.Proportional, result.Proportional);
            Assert.AreEqual(a.MaxOutput, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Add_SumsAllFields()
        {
            var a = MakeA();
            var b = MakeB();
            var result = PidMixer.Add(a, b);

            Assert.AreEqual(new float3(1, 1, 0), result.Proportional);
            Assert.AreEqual(new float3(2, 2, 0), result.Derivative);
            Assert.AreEqual(new float3(3, 3, 0), result.Integral);
            Assert.AreEqual(30f, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Add_WithZero_NoChange()
        {
            var a = MakeA();
            var z = new PidTuning();
            var result = PidMixer.Add(a, z);

            Assert.AreEqual(a.Proportional, result.Proportional);
            Assert.AreEqual(a.Derivative, result.Derivative);
            Assert.AreEqual(a.Integral, result.Integral);
            Assert.AreEqual(a.MaxOutput, result.MaxOutput, 0.001f);
        }

        [Test]
        public void Add_Commutative()
        {
            var a = MakeA();
            var b = MakeB();
            var ab = PidMixer.Add(a, b);
            var ba = PidMixer.Add(b, a);

            Assert.AreEqual(ab.Proportional, ba.Proportional);
            Assert.AreEqual(ab.Derivative, ba.Derivative);
            Assert.AreEqual(ab.Integral, ba.Integral);
            Assert.AreEqual(ab.MaxOutput, ba.MaxOutput, 0.001f);
        }
    }
}