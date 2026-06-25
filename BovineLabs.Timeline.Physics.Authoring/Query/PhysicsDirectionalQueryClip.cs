using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Classify the selected contact's bearing/distance into a directional sector or band and route that as the
    /// condition value (the knockback-ring pattern). Focused replacement for the DirectionSector/DistanceBand use of
    /// the legacy PhysicsTriggerQueryClip; bakes <see cref="PhysicsTriggerQueryData"/> with the sector value config.
    /// </summary>
    public sealed class PhysicsDirectionalQueryClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Stay;
        public PhysicsCategoryTags collidesWith;

        [Tooltip("Which contact is classified. Nearest is the usual choice.")]
        public PhysicsTriggerQuerySelection selection = PhysicsTriggerQuerySelection.Nearest;

        [Tooltip("Maximum query distance. 0 = unlimited.")]
        public float maxDistance;

        [Header("Value")]
        public PhysicsTriggerQueryValueMode valueMode = PhysicsTriggerQueryValueMode.DirectionSector;

        [Tooltip("DirectionSector bin count (4 / 8 / 16).")]
        public int sectorCount = 8;

        public PhysicsTriggerSectorReference sectorReference = PhysicsTriggerSectorReference.SelfForward;
        public PhysicsTriggerSectorPlane sectorPlane = PhysicsTriggerSectorPlane.XZ;
        public Vector3 sectorCustomUp = Vector3.up;

        [Tooltip("Schmitt deadband in radians around a sector boundary. <= 0 uses ~0.15 * binWidth.")]
        public float sectorHysteresis = -1f;

        [Tooltip("DistanceBand: ascending distance thresholds (metres). distSq is bucketed against their squares.")]
        public float[] distanceBands = Array.Empty<float>();

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
                MaxAngleDegrees = 0f, RouteTo = routeTo, RouteLink = routeLink, RouteSlot = routeSlot,
                WriteMode = writeMode, ClearOnLost = clearOnLost, GraceFrames = 0,
                FoundCondition = foundCondition, FoundValue = foundValue, LostCondition = lostCondition,
                LostValue = lostValue, IgnoreTarget = ignoreTarget, RequireLinks = requireLinks,
            };

            var resolvedHysteresis = sectorHysteresis >= 0f
                ? sectorHysteresis
                : PhysicsTriggerSectorMath.DefaultHysteresis(math.max(sectorCount, 1));

            var data = new PhysicsTriggerQueryData
            {
                Selection = selection,
                ValueMode = valueMode,
                SectorCount = math.max(sectorCount, 1),
                SectorReference = sectorReference,
                SectorPlane = sectorPlane,
                SectorCustomUp = sectorCustomUp,
                SectorHysteresis = resolvedHysteresis,
                DistanceBands = PhysicsTriggerBakingUtility.BakeDistanceBandBlob(context.Baker, distanceBands),
            };

            var builder = PhysicsQueryClipBaking.Build(context, in common, data);
            builder.ApplyTo(ref commands);
            base.Bake(clipEntity, context);
        }
    }
}
