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

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerConditionClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;

        [Tooltip("Filter by Physics Category. 0/Empty = any collision.")]
        public PhysicsCategoryTags collidesWith;

        public ConditionEventObject condition;
        public int value = 1;

        public Target routeTo = Target.Target;
        public EntityLinkSchema routeLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (!EntityLinkAuthoringUtility.TryGetKey(routeLink, out var linkKey)) linkKey = 0;

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerConditionData
            {
                EventState = triggerState,
                CollidesWithMask = collidesWith.Value,
                Condition = condition ? condition.Key : ConditionKey.Null,
                Value = value,
                RouteTo = routeTo,
                RouteLinkKey = linkKey
            });

            base.Bake(clipEntity, context);
        }
    }
}
