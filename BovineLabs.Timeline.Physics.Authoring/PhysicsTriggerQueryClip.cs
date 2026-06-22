using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerQueryClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Stay;
        public PhysicsCategoryTags collidesWith;

        [Header("Gates")] [Tooltip("Maximum query distance. 0 = unlimited.")]
        public float maxDistance;

        [Tooltip("Maximum angle from the bound entity's forward, in degrees. 0 = ignore.")] [Range(0f, 180f)]
        public float maxAngleDegrees;

        public bool requireLineOfSight;
        public PhysicsCategoryTags obstacles;
        public float lineOfSightOffset = 0.5f;

        [Header("Selection")] public PhysicsTriggerQuerySelection selection = PhysicsTriggerQuerySelection.Nearest;

        [Header("Routing")]
        [Tooltip("Whose Targets.Custom receives the winner. Self routes to the bound entity itself.")]
        public Target routeTo = Target.Self;

        public EntityLinkSchema routeLink;

        [Tooltip("When all candidates are lost, write Entity.Null into the routed Custom slot.")]
        public bool clearOnLost = true;

        [Header("Conditions (Optional)")] public ConditionEventObject foundCondition;
        public int foundValue = 1;
        public ConditionEventObject lostCondition;
        public int lostValue = 1;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links are considered.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            if (routeLink == null || !EntityLinkAuthoringUtility.TryGetKey(routeLink, out var linkKey)) linkKey = 0;

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsTriggerQueryBuilder
            {
                QueryData = new PhysicsTriggerQueryData
                {
                    EventState = triggerState,
                    CollidesWithMask = collidesWith.Value,
                    MaxDistance = maxDistance,
                    MaxAngle = math.radians(maxAngleDegrees),
                    RequireLineOfSight = requireLineOfSight,
                    ObstacleMask = obstacles.Value,
                    LineOfSightOffset = lineOfSightOffset,
                    Selection = selection,
                    RouteTo = routeTo,
                    RouteLinkKey = linkKey,
                    ClearOnLost = clearOnLost,
                    FoundCondition = foundCondition ? foundCondition.Key : ConditionKey.Null,
                    FoundValue = foundValue,
                    LostCondition = lostCondition ? lostCondition.Key : ConditionKey.Null,
                    LostValue = lostValue
                },
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = ignoreTarget,
                    LinkFilterBlob = filterBlob
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}