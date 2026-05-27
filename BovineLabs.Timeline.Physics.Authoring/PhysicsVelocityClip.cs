using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsVelocityClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Instant modes apply velocity exactly once per clip activation and ignore Looping.")]
        public PhysicsVelocityMode mode = PhysicsVelocityMode.SetInstant;
        public Vector3 linearVelocity = Vector3.forward;
        public Vector3 angularVelocity;
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

            context.Baker.AddComponent(clipEntity, new PhysicsVelocityAnimated
            {
                AuthoredData = new PhysicsVelocityData
                {
                    Mode = mode,
                    Linear = linearVelocity,
                    Angular = angularVelocity,
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