using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.PID
{
    public class PhysicsAngularPIDClip : DOTSClip, ITimelineClipAsset
    {
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
        public Vector3 targetRotationEuler = Vector3.zero;

        [Header("Influence")] [Tooltip("Output force multiplier. 0 = no effect, 1 = full, 2 = double.")] [Min(0f)]
        public float strength = 1f;

        [Header("Stop Threshold (Optional)")] [Tooltip("Suppress PID output when angular error (degrees) is below this value. 0 = disabled.")] [Min(0f)]
        public float stopThreshold;

        [Header("Stat Multiplier (Optional)")] public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            context.Baker.AddComponent(clipEntity, new PhysicsAngularPIDAnimated
            {
                AuthoredData = new PhysicsAngularPIDData
                {
                    Tuning = tuning,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetRotation = quaternion.Euler(math.radians(targetRotationEuler)),
                    Strength = strength,
                    StrengthStat = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    },
                    StopThreshold = stopThreshold
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}