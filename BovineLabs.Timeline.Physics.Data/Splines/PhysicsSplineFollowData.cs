using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public enum SplineTraversal : byte
    {
        OverDuration,

        ConstantSpeed
    }

    public enum SplineWrap : byte
    {
        Clamp,
        Loop,
        PingPong
    }

    public struct PhysicsSplineFollowData
    {
        public ushort SplineKey;
        public SplineTraversal Traversal;
        public SplineWrap Wrap;
        public float Speed;
        public float TraversalSeconds;
        public float Lead;
        public PidTuning Tuning;
        public float Strength;
        public StatStrengthConfig StrengthStat;
    }

    public struct PhysicsSplineFollowAnimated : IAnimatedComponent<PhysicsSplineFollowData>
    {
        public PhysicsSplineFollowData AuthoredData;
        [CreateProperty] public PhysicsSplineFollowData Value { get; set; }
    }

    public struct ActiveSplineFollow : IComponentData, IEnableableComponent
    {
        public PhysicsSplineFollowData Config;
    }

    public struct PhysicsSplineFollowState : IComponentData
    {
        public float Progress;
        public PidStateData Pid;
    }

    public readonly struct PhysicsSplineFollowMixer : IMixer<PhysicsSplineFollowData>
    {
        public PhysicsSplineFollowData Lerp(in PhysicsSplineFollowData a, in PhysicsSplineFollowData b, in float s)
        {
            return new PhysicsSplineFollowData
            {
                SplineKey = s < 0.5f ? a.SplineKey : b.SplineKey,
                Traversal = s < 0.5f ? a.Traversal : b.Traversal,
                Wrap = s < 0.5f ? a.Wrap : b.Wrap,
                Speed = math.lerp(a.Speed, b.Speed, s),
                TraversalSeconds = math.lerp(a.TraversalSeconds, b.TraversalSeconds, s),
                Lead = math.lerp(a.Lead, b.Lead, s),
                Tuning = PidMixer.Lerp(a.Tuning, b.Tuning, s),
                Strength = math.lerp(a.Strength, b.Strength, s),
                StrengthStat = s < 0.5f ? a.StrengthStat : b.StrengthStat
            };
        }

        public PhysicsSplineFollowData Add(in PhysicsSplineFollowData a, in PhysicsSplineFollowData b)
        {
            return a;
        }
    }
}