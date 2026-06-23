using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [BurstCompile]
    public static class SplineWrapEval
    {
        public static float Evaluate(float p, SplineWrap wrap)
        {
            switch (wrap)
            {
                case SplineWrap.Loop:
                    return p - math.floor(p);
                case SplineWrap.PingPong:
                    var m = math.abs(p);
                    m -= math.floor(m / 2f) * 2f;
                    return 1f - math.abs(1f - m);
                default:
                    return math.saturate(p);
            }
        }
    }
}
