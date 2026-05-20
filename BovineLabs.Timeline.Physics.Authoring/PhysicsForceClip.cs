using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
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

        [Header("Stat Multiplier (Optional)")] public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            context.Baker.AddComponent(clipEntity, new PhysicsForceAnimated
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    Linear = linearForce,
                    Angular = angularForce,
                    Space = space,
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