using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Nerve.PhysicsStates;
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
    public sealed class PhysicsTriggerForceClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema readStatLink;
        public EntityLinkSchema applyToLink;

        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsTriggerForceType forceType = PhysicsTriggerForceType.Radial;
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Intent = a normal force the target's drag/clamp shape. External = knockback that survives braking " +
                 "and fades on its own (use for hits the player shouldn't be able to brake out of).")]
        public MotionChannel channel = MotionChannel.Intent;

        [Tooltip("Base magnitude. For radial: Positive pulls in (Implosion), Negative pushes out (Explosion).")]
        public float magnitude = 10f;

        [Tooltip("Used only when Force Type is Directional.")]
        public Vector3 direction = Vector3.forward;

        [Header("Radial / Vortex Options")]
        public PhysicsTriggerPositionMode originMode = PhysicsTriggerPositionMode.MatchSelf;

        public PhysicsTriggerFalloffCurve falloffCurve = PhysicsTriggerFalloffCurve.None;
        [Min(0f)] public float falloffStartRadius;
        [Min(0f)] public float falloffEndRadius = 5f;

        [Header("Stat Multiplier (Optional)")]
        [Tooltip("If set, multiplies the base magnitude by the target's stat value.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;

        [Header("Apply To Target")] public Target applyTo = Target.Target;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        [Tooltip(
            "AllContacts applies once per contacting collider; FirstPerRoot applies once per enemy (resolved root).")]
        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.AllContacts;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var bakedState = triggerState;
            if (mode == PhysicsForceMode.Continuous && bakedState != StatefulEventState.Stay)
                bakedState = StatefulEventState.Stay;

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsTriggerForceBuilder
            {
                ForceData = new PhysicsTriggerForceData
                {
                    EventState = bakedState,
                    ForceType = forceType,
                    Mode = mode,
                    Channel = channel,
                    Magnitude = magnitude,
                    Direction = math.normalizesafe(direction, new float3(0, 0, 1)),
                    OriginMode = originMode,
                    FalloffCurve = falloffCurve,
                    FalloffStartRadius = falloffStartRadius,
                    FalloffEndRadius = falloffEndRadius,
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