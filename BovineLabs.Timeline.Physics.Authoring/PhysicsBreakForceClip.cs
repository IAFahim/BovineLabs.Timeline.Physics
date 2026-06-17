using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    ///     "Break force" clip for the Stateful Trigger track. When a body touches the bound trigger source, measure
    ///     its momentum: at or under a (stat-scaled) threshold it is caught — stopped, reversed, or redirected; above
    ///     it, it breaks through untouched. The shield strength is a live stat, so it can be powered up or drained.
    /// </summary>
    public sealed class PhysicsBreakForceClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsBreakMode mode = PhysicsBreakMode.Brake;

        [Header("Break")]
        [Tooltip("Momentum (speed × mass) the break can absorb. 0 = unbreakable (always catches).")]
        [Min(0f)] public float threshold = 50f;

        [Tooltip("0 = dead stop · 1 = full reverse / full-speed return · >1 = amplified return.")]
        [Min(0f)] public float restitution;

        [Header("Redirect (degrees)")]
        [Tooltip("Angle around the source's up axis.")]
        public float azimuth;

        [Tooltip("Angle above the source's forward.")]
        public float elevation;

        [Header("Shield Strength Stat (optional)")]
        [Tooltip("If set, multiplies the threshold — a live shield-strength stat, resolved through Targets/EntityLinks.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        [Header("Apply To")]
        [Tooltip("Whom to push. Target = the contacting body (the arrow).")]
        public Target applyTo = Target.Target;

        public EntityLinkSchema applyToLink;

        [Header("Filtering")]
        [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the break.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.FirstPerRoot;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
            {
                readStatKey = k1;
            }

            ushort applyToKey = 0;
            if (applyToLink != null && EntityLinkAuthoringUtility.TryGetKey(applyToLink, out var k2))
            {
                applyToKey = k2;
            }

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsBreakForceBuilder
            {
                BreakData = new PhysicsBreakForceData
                {
                    EventState = triggerState,
                    Mode = mode,
                    BaseThreshold = threshold,
                    Restitution = restitution,
                    Azimuth = math.radians(azimuth),
                    Elevation = math.radians(elevation),
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey,
                    },
                    ApplyTo = applyTo,
                    ApplyToLinkKey = applyToKey,
                },
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = ignoreTarget,
                    LinkFilterBlob = filterBlob,
                    HitMode = hitMode,
                },
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}
