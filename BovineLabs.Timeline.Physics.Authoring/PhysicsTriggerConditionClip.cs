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
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerConditionClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsCategoryTags collidesWith;
        public ConditionEventObject condition;
        public int value = 1;
        public EntityLinkSchema routeLink;
        public Target routeTo = Target.Target;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        [Tooltip("AllContacts fires once per contacting collider; FirstPerRoot fires once per enemy (resolved root).")]
        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.AllContacts;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsTriggerConditionBuilder
            {
                ConditionData = new PhysicsTriggerConditionData
                {
                    EventState = triggerState,
                    CollidesWithMask = collidesWith.Value,
                    Condition = condition ? new ConditionKey(condition.Key) : ConditionKey.Null,
                    Value = value,
                    RouteTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, routeLink, routeTo)
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