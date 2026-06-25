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
    /// Fan out to every gated survivor (AoE boom) or the Top-K of them, firing the found condition per winner.
    /// Focused replacement for the multi-winner use of the legacy PhysicsTriggerQueryClip; bakes
    /// <see cref="PhysicsTriggerQueryData"/> with a multi-winner selection.
    /// </summary>
    public sealed class PhysicsAoEQueryClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Stay;
        public PhysicsCategoryTags collidesWith;

        [Tooltip("AllSurvivorsFanout (everyone) or TopK (the best N by proximity).")]
        public PhysicsTriggerQuerySelection selection = PhysicsTriggerQuerySelection.AllSurvivorsFanout;

        [Tooltip("Hard cap on winners (1..8). Survivors past the cap are dropped.")] [Range(1, 8)]
        public int maxTargets = 8;

        [Tooltip("Maximum query distance. 0 = unlimited.")]
        public float maxDistance;

        [Tooltip("Also emit a capped DynamicBuffer<TriggerQueryHit> on the routed entity.")]
        public bool writeHitBuffer;

        [Header("Routing")]
        public Target routeTo = Target.Self;
        public EntityLinkSchema routeLink;
        public PhysicsTriggerRouteSlot routeSlot = PhysicsTriggerRouteSlot.Custom;
        public PhysicsTriggerWriteMode writeMode = PhysicsTriggerWriteMode.Set;

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
                WriteMode = writeMode, ClearOnLost = false, GraceFrames = 0,
                FoundCondition = foundCondition, FoundValue = foundValue, LostCondition = lostCondition,
                LostValue = lostValue, IgnoreTarget = ignoreTarget, RequireLinks = requireLinks,
            };

            var data = new PhysicsTriggerQueryData
            {
                Selection = selection,
                MaxTargets = math.clamp(maxTargets, 1, 8),
                WriteHitBuffer = writeHitBuffer,
                ValueMode = PhysicsTriggerQueryValueMode.Constant,
            };

            var builder = PhysicsQueryClipBaking.Build(context, in common, data);
            builder.ApplyTo(ref commands);
            base.Bake(clipEntity, context);
        }
    }
}
