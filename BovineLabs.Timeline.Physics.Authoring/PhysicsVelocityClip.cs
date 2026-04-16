using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsVelocityClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Linear velocity added (accel mode) per second")]
        public Vector3 linearVelocity = Vector3.forward;

        [Tooltip("Angular velocity added (accel mode) per second")]
        public Vector3 angularVelocity;
        
        public Target space = Target.None;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsVelocityAnimated
            {
                AuthoredVelocity = new PhysicsVelocityData
                {
                    Linear = linearVelocity,
                    Angular = angularVelocity,
                    Space = space
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}
