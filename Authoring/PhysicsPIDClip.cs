using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        [Tooltip("0 = Fly relative to self. 1 = Chase Reaction Target entity.")]
        [Range(0f, 1f)] public float ChaseTargetBlend = 0f;
        [Tooltip("The offset from either self (if blend=0) or target (if blend=1).")]
        public Vector3 LocalTargetOffset = new Vector3(0, 0, 10f);

        [Header("PID Tuning")]
        public Vector3 Proportional = new Vector3(10f, 10f, 10f);
        public Vector3 Integral = new Vector3(2f, 2f, 2f);
        public Vector3 Derivative = new Vector3(1f, 1f, 1f);
        public float MaxForce = 100f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsPIDAnimated
            {
                AuthoredData = new PhysicsPIDData
                {
                    Proportional = Proportional,
                    Integral = Integral,
                    Derivative = Derivative,
                    LocalTargetOffset = LocalTargetOffset,
                    ChaseTargetBlend = ChaseTargetBlend,
                    MaxForce = MaxForce
                }
            });

            // We DO NOT touch the boundTarget here. The Baking System handles it.
            base.Bake(clipEntity, context);
        }
    }
}