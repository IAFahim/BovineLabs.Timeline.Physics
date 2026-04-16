using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsForceClip : DOTSClip, ITimelineClipAsset
    {
        public Vector3 linearForce;
        public Vector3 angularForce;
        public Target space = Target.None;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsForceAnimated
            {
                AuthoredData = new PhysicsForceData
                {
                    Linear = linearForce,
                    Angular = angularForce,
                    Space = space
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}
