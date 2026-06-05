namespace BovineLabs.Timeline.Physics.Authoring
{
    using System;
    using BovineLabs.Core.PhysicsStates;
    using BovineLabs.Reaction.Authoring.Conditions;
    using BovineLabs.Reaction.Data.Conditions;
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Timeline.Authoring;
    using BovineLabs.Timeline.EntityLinks.Authoring;
    using Unity.Entities;
    using Unity.Physics.Authoring;
    using UnityEngine;
    using UnityEngine.Timeline;

    public sealed class PhysicsTriggerConditionClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsCategoryTags collidesWith;
        public ConditionEventObject condition;
        public int value = 1;
        public Target routeTo = Target.Target;
        public EntityLinkSchema routeLink;

        [Header("Filtering")]
        [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;
        
        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (routeLink == null || !EntityLinkAuthoringUtility.TryGetKey(routeLink, out var linkKey)) linkKey = 0;

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerConditionData
            {
                EventState = triggerState,
                CollidesWithMask = collidesWith.Value,
                Condition = condition ? condition.Key : ConditionKey.Null,
                Value = value,
                RouteTo = routeTo,
                RouteLinkKey = linkKey
            });

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerFilterData
            {
                IgnoreTarget = ignoreTarget,
                LinkFilterBlob = filterBlob
            });

            base.Bake(clipEntity, context);
        }
    }
}
