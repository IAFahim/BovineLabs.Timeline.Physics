using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Pick one target from the contacts/overlaps and route it into a Targets slot ("find the enemy"). Focused
    /// replacement for the single-winner use of the legacy PhysicsTriggerQueryClip; bakes
    /// <see cref="PhysicsTriggerQueryData"/> with the chosen geometric selection and no gates/value extras.
    /// </summary>
    public sealed class PhysicsTargetSelectClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Stay;
        public PhysicsCategoryTags collidesWith;

        [Header("Selection")]
        public PhysicsTriggerQuerySelection selection = PhysicsTriggerQuerySelection.Nearest;

        [Tooltip("Maximum query distance. 0 = unlimited.")]
        public float maxDistance;

        [Tooltip("Maximum angle from the bound entity's forward, in degrees. 0 = ignore.")] [Range(0f, 180f)]
        public float maxAngleDegrees;

        public bool requireLineOfSight;
        public PhysicsCategoryTags obstacles;
        public float lineOfSightOffset = 0.5f;

        [Header("Stability (Optional)")]
        [Tooltip("StickyWinner: a challenger only displaces the incumbent when it beats its re-scored score by this "
                 + "margin (×100). 0 = no stickiness.")]
        public int switchMargin;

        [Tooltip("LostDebounce: hold the last winner this many query frames before clearing/firing lost. 0 = immediate.")]
        public int graceFrames;

        [Header("Routing")]
        public Target routeTo = Target.Self;
        public EntityLinkSchema routeLink;
        public PhysicsTriggerRouteSlot routeSlot = PhysicsTriggerRouteSlot.Custom;
        public PhysicsTriggerWriteMode writeMode = PhysicsTriggerWriteMode.Set;
        public bool clearOnLost = true;

        [Header("Conditions (Optional)")]
        public ConditionEventObject foundCondition;
        public int foundValue = 1;
        public ConditionEventObject lostCondition;
        public int lostValue = 1;

        [Header("Filtering")]
        public Target ignoreTarget = Target.Owner;
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var common = new PhysicsQueryClipBaking.Common
            {
                TriggerState = triggerState, CollidesWith = collidesWith, MaxDistance = maxDistance,
                MaxAngleDegrees = maxAngleDegrees, RouteTo = routeTo, RouteLink = routeLink, RouteSlot = routeSlot,
                WriteMode = writeMode, ClearOnLost = clearOnLost, GraceFrames = graceFrames,
                FoundCondition = foundCondition, FoundValue = foundValue, LostCondition = lostCondition,
                LostValue = lostValue, IgnoreTarget = ignoreTarget, RequireLinks = requireLinks,
            };

            var data = new PhysicsTriggerQueryData
            {
                Selection = selection,
                SwitchMargin = switchMargin,
                RequireLineOfSight = requireLineOfSight,
                ObstacleMask = obstacles.Value,
                LineOfSightOffset = lineOfSightOffset,
                ValueMode = PhysicsTriggerQueryValueMode.Constant,
            };

            var builder = PhysicsQueryClipBaking.Build(context, in common, data);
            builder.ApplyTo(ref commands);
            base.Bake(clipEntity, context);
        }
    }
}
