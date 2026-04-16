using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsDragClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Linear drag multiplier. 0 = no drag. 50 = instant stop (at 50hz).")]
        public float linearDrag = 5f;
        
        [Tooltip("Angular drag multiplier. 0 = no drag. 50 = instant stop (at 50hz).")]
        public float angularDrag = 5f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsDragAnimated
            {
                AuthoredData = new PhysicsDragData
                {
                    Linear = linearDrag,
                    Angular = angularDrag
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}
