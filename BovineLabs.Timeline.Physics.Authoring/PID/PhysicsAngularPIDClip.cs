using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
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

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsAngularPIDAnimated
            {
                AuthoredData = new PhysicsAngularPIDData
                {
                    Tuning = tuning,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetRotation = quaternion.Euler(math.radians(targetRotationEuler)),
                    Strength = strength
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}