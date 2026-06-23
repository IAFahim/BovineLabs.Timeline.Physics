using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class SplineWrapEvalTests
    {
        [Test]
        public void Loop_WrapsAtIntegerBoundary()
        {
            Assert.AreEqual(0f, SplineWrapEval.Evaluate(0f, SplineWrap.Loop), 1e-5f);
            Assert.AreEqual(0f, SplineWrapEval.Evaluate(1f, SplineWrap.Loop), 1e-5f);
        }

        [Test]
        public void PingPong_FoldsForwardThenBack()
        {
            Assert.AreEqual(0.25f, SplineWrapEval.Evaluate(0.25f, SplineWrap.PingPong), 1e-5f);
            Assert.AreEqual(0.75f, SplineWrapEval.Evaluate(0.75f, SplineWrap.PingPong), 1e-5f);
            Assert.AreEqual(0.75f, SplineWrapEval.Evaluate(1.25f, SplineWrap.PingPong), 1e-5f);
            Assert.AreEqual(0.25f, SplineWrapEval.Evaluate(1.75f, SplineWrap.PingPong), 1e-5f);
            Assert.AreEqual(0f, SplineWrapEval.Evaluate(2f, SplineWrap.PingPong), 1e-5f);
        }

        [Test]
        public void Clamp_SaturatesOutOfRange()
        {
            Assert.AreEqual(0f, SplineWrapEval.Evaluate(-0.5f, SplineWrap.Clamp), 1e-5f);
            Assert.AreEqual(1f, SplineWrapEval.Evaluate(1.5f, SplineWrap.Clamp), 1e-5f);
        }

        [Test]
        public void Loop_NaNInput_PassesThroughAsNaN()
        {
            Assert.IsTrue(math.isnan(SplineWrapEval.Evaluate(float.NaN, SplineWrap.Loop)));
        }
    }
}
