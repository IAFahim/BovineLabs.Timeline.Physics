using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>How the follow target advances along the spline.</summary>
    public enum SplineTraversal : byte
    {
        /// <summary>Traverse the whole path in <c>TraversalSeconds</c> (param speed = 1/seconds).</summary>
        OverDuration,

        /// <summary>Traverse at <c>Speed</c> metres/second along arc length (uses the blob's Length).</summary>
        ConstantSpeed,
    }

    /// <summary>What happens when progress passes the end of the path.</summary>
    public enum SplineWrap : byte
    {
        Clamp,
        Loop,
        PingPong,
    }

    public struct PhysicsSplineFollowData
    {
        public ushort SplineKey;
        public SplineTraversal Traversal;
        public SplineWrap Wrap;
        public float Speed;            // metres/second, ConstantSpeed mode
        public float TraversalSeconds; // seconds for the whole path, OverDuration mode
        public float Lead;             // 0..~0.3 look-ahead fraction (aims the PID ahead — the racing line)
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
        public float Progress; // path parameter accumulator (grows unbounded for Loop/PingPong; wrapped at eval)
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
                StrengthStat = s < 0.5f ? a.StrengthStat : b.StrengthStat,
            };
        }

        // A body follows ONE path; overlapping follow clips don't sum meaningfully — keep the dominant (first) config.
        public PhysicsSplineFollowData Add(in PhysicsSplineFollowData a, in PhysicsSplineFollowData b)
        {
            return a;
        }
    }
}
