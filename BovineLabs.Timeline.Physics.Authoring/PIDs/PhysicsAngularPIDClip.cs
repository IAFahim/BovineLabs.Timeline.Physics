using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.PIDs
{
    public class PhysicsAngularPIDClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema readStatLink;

        [Header("Gains")] public bool uniformAxes = true;

        public PidTuning tuning = new()
        {
            Proportional = new Vector3(10f, 10f, 10f),
            Integral = new Vector3(2f, 2f, 2f),
            Derivative = new Vector3(1f, 1f, 1f),
            MaxOutput = 100f
        };

        [Header("Destination")] public Target trackingTarget = Target.Target;

        public PidAngularTargetMode targetMode = PidAngularTargetMode.LookAtTarget;

        [Tooltip(
            "In World mode this is the ABSOLUTE world-space rotation (euler). In other modes it is an offset from the tracking target.")]
        public Vector3 targetRotationEuler = Vector3.zero;

        [Header("Influence")] [Tooltip("Output force multiplier. 0 = no effect, 1 = full, 2 = double.")] [Min(0f)]
        public float strength = 1f;

        [Header("Stat Multiplier (Optional)")] public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var builder = new PhysicsAngularPIDBuilder
            {
                AuthoredData = new PhysicsAngularPIDData
                {
                    Tuning = tuning,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetRotation = quaternion.Euler(math.radians(targetRotationEuler)),
                    Strength = strength,
                    StrengthStat = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}