using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsVelocityClampClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Maximum linear speed. Set to negative to ignore.")]
        public float maxLinearSpeed = 10f;

        [Tooltip("Maximum angular speed (radians per second). Set to negative to ignore.")]
        public float maxAngularSpeed = -1f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsVelocityClampAnimated
            {
                AuthoredData = new PhysicsVelocityClampData
                {
                    MaxLinearSpeed = maxLinearSpeed,
                    MaxAngularSpeed = maxAngularSpeed
                }
            });
            
            base.Bake(clipEntity, context);
        }
    }
}
