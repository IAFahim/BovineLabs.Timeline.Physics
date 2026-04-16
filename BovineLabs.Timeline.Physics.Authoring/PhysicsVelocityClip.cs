// BovineLabs.Timeline.Physics.Authoring/PhysicsVelocityClip.cs
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsVelocityClip : DOTSClip, ITimelineClipAsset
    {
        public Vector3 linearVelocity = Vector3.forward;
        public Vector3 angularVelocity;
        public Target space = Target.None;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsVelocityAnimated
            {
                AuthoredData = new PhysicsVelocityData
                {
                    Linear = this.linearVelocity,
                    Angular = this.angularVelocity,
                    Space = this.space
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}