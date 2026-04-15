using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsLinearPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        [Tooltip("0 = Self offset. 1 = Reaction Target entity. Blend for partial.")]
        [Range(0f, 1f)]
        public float chaseTargetBlend;

        public Vector3 localTargetOffset = new(0, 0, 10f);

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
                    LocalTargetOffset = localTargetOffset,
                    ChaseTargetBlend = chaseTargetBlend,
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}