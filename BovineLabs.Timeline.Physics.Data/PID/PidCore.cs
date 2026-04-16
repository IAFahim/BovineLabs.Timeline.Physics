using System;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    [Serializable]
    public struct PidTuning
    {
        public float3 Proportional;
        public float3 Integral;
        public float3 Derivative;
        public float MaxOutput;
    }

    public struct PidStateData
    {
        public float3 IntegralAccumulator;
        public float3 PreviousError;
        public bool IsInitialized;
    }

    public static class PidMixer
    {
        public static PidTuning Lerp(in PidTuning a, in PidTuning b, float s) => new()
        {
            Proportional = math.lerp(a.Proportional, b.Proportional, s),
            Integral = math.lerp(a.Integral, b.Integral, s),
            Derivative = math.lerp(a.Derivative, b.Derivative, s),
            MaxOutput = math.lerp(a.MaxOutput, b.MaxOutput, s)
        };

        public static PidTuning Add(in PidTuning a, in PidTuning b) => new()
        {
            Proportional = a.Proportional + b.Proportional,
            Integral = a.Integral + b.Integral,
            Derivative = a.Derivative + b.Derivative,
            MaxOutput = a.MaxOutput + b.MaxOutput
        };
    }
}
