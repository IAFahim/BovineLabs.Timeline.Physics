using System;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [Serializable]
    public struct PidTuning
    {
        public float3 Proportional;
        public float3 Derivative;   // D before I — tune in this order
        public float3 Integral;
        public float  MaxOutput;
    }

    public struct PidStateData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public float3 CapturedTargetPosition; // InitialLocal mode: locked on first tick
        public bool   IsInitialized;
    }

    public static class PidMixer
    {
        public static PidTuning Lerp(in PidTuning a, in PidTuning b, float s) => new()
        {
            Proportional = math.lerp(a.Proportional, b.Proportional, s),
            Derivative   = math.lerp(a.Derivative,   b.Derivative,   s),
            Integral     = math.lerp(a.Integral,     b.Integral,     s),
            MaxOutput    = math.lerp(a.MaxOutput,    b.MaxOutput,    s),
        };

        public static PidTuning Add(in PidTuning a, in PidTuning b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Derivative   = a.Derivative   + b.Derivative,
            Integral     = a.Integral     + b.Integral,
            MaxOutput    = a.MaxOutput    + b.MaxOutput,
        };
    }
}
