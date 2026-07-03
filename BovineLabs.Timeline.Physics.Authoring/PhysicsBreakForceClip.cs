using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsBreakForceClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema readStatLink;
        public EntityLinkSchema applyToLink;

        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsBreakMode mode = PhysicsBreakMode.Brake;

        [Header("Break")]
        [Tooltip("Momentum (speed × mass) the break can absorb. 0 = unbreakable (always catches).")]
        [Min(0f)]
        public float threshold = 50f;

        [Tooltip("0 = dead stop · 1 = full reverse / full-speed return · >1 = amplified return.")] [Min(0f)]
        public float restitution;

        [Header("Redirect (degrees)")] [Tooltip("Angle around the source's up axis.")]
        public float azimuth;

        [Tooltip("Angle above the source's forward.")]
        public float elevation;

        [Header("Shield Strength Stat (optional)")]
        [Tooltip(
            "If set, multiplies the threshold — a live shield-strength stat, resolved through Targets/EntityLinks.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;

        [Header("Apply To")] [Tooltip("Whom to push. Target = the contacting body (the arrow).")]
        public Target applyTo = Target.Target;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the break.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.FirstPerRoot;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

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
                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    },
                    ApplyTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, applyToLink, applyTo)
                },
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = ignoreTarget,
                    LinkFilterBlob = filterBlob,
                    HitMode = hitMode
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}