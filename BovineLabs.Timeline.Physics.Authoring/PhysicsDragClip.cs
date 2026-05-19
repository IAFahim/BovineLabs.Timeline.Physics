using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
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

        [Header("Stat Multiplier (Optional)")]
        public StatSchemaObject strengthStat;
        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1)) 
                readStatKey = k1;

            context.Baker.AddComponent(clipEntity, new PhysicsDragAnimated
            {
                AuthoredData = new PhysicsDragData
                {
                    Linear = linearDrag,
                    Angular = angularDrag,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}