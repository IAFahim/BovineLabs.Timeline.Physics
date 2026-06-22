using System;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [Serializable]
    public struct PidTuning
    {
        public float3 Proportional;
        public float3 Derivative;
        public float3 Integral;
        public float MaxOutput;
    }

    public struct PidStateData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public float3 CapturedTargetPosition;
        public bool IsInitialized;
    }

    public static class PidMixer
    {
        public static PidTuning Lerp(in PidTuning a, in PidTuning b, float s)
        {
            return new PidTuning
            {
                Proportional = math.lerp(a.Proportional, b.Proportional, s),
                Derivative = math.lerp(a.Derivative, b.Derivative, s),
                Integral = math.lerp(a.Integral, b.Integral, s),
                MaxOutput = math.lerp(a.MaxOutput, b.MaxOutput, s)
            };
        }

        public static PidTuning Add(in PidTuning a, in PidTuning b)
        {
            return new PidTuning
            {
                Proportional = a.Proportional + b.Proportional,
                Derivative = a.Derivative + b.Derivative,
                Integral = a.Integral + b.Integral,
                MaxOutput = a.MaxOutput + b.MaxOutput
            };
        }

        public static quaternion AddRotation(in quaternion a, in quaternion b)
        {
            return Exp(Log(a) + Log(b));
        }

        private static float3 Log(in quaternion q)
        {
            var v = math.select(q.value, -q.value, q.value.w < 0f);
            var n = math.length(v.xyz);
            var angle = 2f * math.atan2(n, v.w);
            return math.select(float3.zero, v.xyz / n * angle, n > 1e-6f);
        }

        private static quaternion Exp(in float3 r)
        {
            var angle = math.length(r);
            return angle < 1e-6f ? quaternion.identity : quaternion.AxisAngle(r / angle, angle);
        }
    }
}