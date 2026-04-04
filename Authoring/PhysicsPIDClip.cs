using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Targeting")]
        [Tooltip("Leave empty to use the Reaction 'Targets' component dynamically.")]
        public ExposedReference<GameObject> ExplicitTarget;
        
        [Header("Offset")]
        public Vector3 TargetOffset;
        [Tooltip("If true, the offset rotates with the Target's rotation.")]
        public bool IsLocalOffset = true;

        [Header("PID Tuning")]
        [Tooltip("P: Immediate force towards the target.")]
        public Vector3 Proportional = new Vector3(10f, 10f, 10f);
        [Tooltip("I: Builds up force over time if blocked by a wall.")]
        public Vector3 Integral = new Vector3(2f, 2f, 2f);
        [Tooltip("D: Dampens speed as it approaches the target to prevent overshoot.")]
        public Vector3 Derivative = new Vector3(1f, 1f, 1f);
        
        public float MaxForce = 50f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var targetGo = ExplicitTarget.Resolve(context.Director);
            var targetEntity = targetGo != null ? context.Baker.GetEntity(targetGo, TransformUsageFlags.Dynamic) : Entity.Null;

            context.Baker.AddComponent(clipEntity, new PhysicsPIDAnimated
            {
                ExplicitTarget = targetEntity,
                UseReactionTargets = targetEntity == Entity.Null,
                IsLocalOffset = IsLocalOffset,
                AuthoredData = new PhysicsPIDData
                {
                    Proportional = Proportional,
                    Integral = Integral,
                    Derivative = Derivative,
                    Offset = TargetOffset,
                    MaxForce = MaxForce
                }
            });

            // Ensure the missile has the state component required to accumulate PID memory
            var boundTarget = context.Binding != null ? context.Binding.Target : Entity.Null;
            if (boundTarget != Entity.Null)
            {
                context.Baker.AddComponent<PhysicsPIDState>(boundTarget);
            }

            base.Bake(clipEntity, context);
        }
    }
}