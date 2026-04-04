using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Targeting (Local Carrot)")]
        [Tooltip("Where the entity wants to go relative to its CURRENT position and rotation. E.g., (0,0,10) means 'Drive forward'.")]
        public Vector3 LocalTargetOffset = new Vector3(0, 0, 10f);

        [Header("PID Tuning")]
        [Tooltip("P: Immediate force towards the target point.")]
        public Vector3 Proportional = new Vector3(10f, 10f, 10f);
        [Tooltip("I: Builds up force over time if blocked by a wall or drag.")]
        public Vector3 Integral = new Vector3(2f, 2f, 2f);
        [Tooltip("D: Dampens speed as it approaches the target to prevent overshoot.")]
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
                    MaxForce = MaxForce
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}