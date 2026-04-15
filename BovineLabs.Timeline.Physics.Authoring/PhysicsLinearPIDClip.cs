using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsLinearPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        public Target trackingTarget = Target.Target;
        public PidLinearTargetMode targetMode = PidLinearTargetMode.TargetLocal;
        public Vector3 targetOffset = new(0, 0, 0);

        [Header("Linear Tuning")]
        public bool uniformAxes = true;
        public Vector3 proportional = new(10f, 10f, 10f);
        public Vector3 integral = new(2f, 2f, 2f);
        public Vector3 derivative = new(1f, 1f, 1f);
        public float maxForce = 100f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsLinearPIDAnimated
            {
                AuthoredData = new PhysicsLinearPIDData
                {
                    Proportional = proportional,
                    Integral     = integral,
                    Derivative   = derivative,
                    MaxForce = maxForce,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetOffset = targetOffset,
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}