using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsForceClip : DOTSClip, ITimelineClipAsset
    {
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;
        public Vector3 linearForce = new(0, 0, 0);
        public Vector3 angularForce;
        public Target space = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsForceAnimated
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    Linear = linearForce,
                    Angular = angularForce,
                    Space = space
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}