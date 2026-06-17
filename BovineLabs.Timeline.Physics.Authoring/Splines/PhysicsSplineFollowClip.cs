using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    /// <summary>
    ///     Force-traces the bound physics body along a <see cref="SplineSchema" />'s path: a PID motor whose target
    ///     point advances along the spline as the clip plays. Composes with gravity/collisions (it writes forces, not
    ///     transforms). Strength is stat-driven, so an intrinsic/stat can scale or cut the pull at runtime.
    /// </summary>
    public class PhysicsSplineFollowClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Path")]
        [Tooltip("Which path to follow. A SplinePathAuthoring GameObject must register this same schema.")]
        public SplineSchema spline;

        public SplineTraversal traversal = SplineTraversal.OverDuration;
        public SplineWrap wrap = SplineWrap.Clamp;

        [Header("Speed")]
        [Tooltip("ConstantSpeed mode: metres/second along the path.")]
        public float speed = 10f;

        [Tooltip("OverDuration mode: seconds to traverse the whole path.")]
        public float traversalSeconds = 2f;

        [Header("Feel")]
        [Tooltip("Aims the motor this fraction ahead on the path — the racing line. 0 = chase the exact point.")]
        [Range(0f, 0.3f)]
        public float lead = 0.05f;

        public PidTuning tuning = new()
        {
            Proportional = new float3(20f, 20f, 20f),
            Derivative = new float3(4f, 4f, 4f),
            Integral = float3.zero,
            MaxOutput = 200f,
        };

        [Header("Influence")]
        [Min(0f)] public float strength = 1f;

        [Tooltip("Optional stat that scales the pull (×100 encoded). Resolved through Targets / EntityLinks.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 2;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            ushort statLinkKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k))
            {
                statLinkKey = k;
            }

            var builder = new PhysicsSplineFollowBuilder
            {
                AuthoredData = new PhysicsSplineFollowData
                {
                    SplineKey = spline != null ? spline.Id : (ushort)0,
                    Traversal = traversal,
                    Wrap = wrap,
                    Speed = speed,
                    TraversalSeconds = traversalSeconds,
                    Lead = lead,
                    Tuning = tuning,
                    Strength = strength,
                    StrengthStat = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = statLinkKey,
                    },
                },
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}
