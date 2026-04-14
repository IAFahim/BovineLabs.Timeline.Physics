using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsVelocityClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Linear velocity in world units per second")]
        public Vector3 linearVelocity = Vector3.forward;

        [Tooltip("Angular velocity in radians per second")]
        public Vector3 angularVelocity;

        public bool isLocalSpace;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsVelocityAnimated
            {
                AuthoredVelocity = new PhysicsVelocityData
                {
                    Linear = linearVelocity,
                    Angular = angularVelocity
                },
                IsLocalSpace = isLocalSpace
            });

            base.Bake(clipEntity, context);
        }
    }
}