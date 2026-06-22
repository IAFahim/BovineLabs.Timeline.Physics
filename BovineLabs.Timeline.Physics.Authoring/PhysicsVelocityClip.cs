using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
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

        [Tooltip("Zeroes the body's velocity once per clip activation, immediately before the Add lands. " +
                 "Use Linear for dashes that must always travel the same distance. Ignored by Set modes, " +
                 "which already replace the velocity outright.")]
        public VelocityResetFlags resetVelocityOnFire = VelocityResetFlags.None;

        [Header("Stat Multiplier (Optional)")] public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            var builder = new PhysicsVelocityBuilder
            {
                AuthoredData = new PhysicsVelocityData
                {
                    Mode = mode,
                    Linear = linearVelocity,
                    Angular = angularVelocity,
                    Space = space,
                    ResetVelocityOnFire = resetVelocityOnFire,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}