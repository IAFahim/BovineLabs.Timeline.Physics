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
        [Header("Destination")]
        public Target trackingTarget = Target.Target;
        public PidAngularTargetMode targetMode = PidAngularTargetMode.LookAtTarget;
        public Vector3 targetRotationEuler = Vector3.zero;

        [Header("Angular Tuning")]
        public bool uniformAxes = true;
        public Vector3 proportional = new(10f, 10f, 10f);
        public Vector3 integral = new(2f, 2f, 2f);
        public Vector3 derivative = new(1f, 1f, 1f);
        public float maxTorque = 100f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsAngularPIDAnimated
            {
                AuthoredData = new PhysicsAngularPIDData
                {
                    Proportional = proportional,
                    Integral     = integral,
                    Derivative   = derivative,
                    MaxTorque = maxTorque,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetRotationEuler = math.radians(targetRotationEuler),
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}