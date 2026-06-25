using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Uniform directional push along a self-relative direction (thrust / launch). Focused replacement for the
    /// Directional mode of the legacy PhysicsTriggerForceClip; bakes <see cref="PhysicsTriggerForceData"/> with
    /// ForceType = Directional. No origin/falloff — a directional thrust is spatially uniform.
    /// </summary>
    public sealed class PhysicsThrustClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        public float magnitude = 10f;

        [Tooltip("Direction in the source's local space; normalised at bake.")]
        public Vector3 direction = Vector3.forward;

        [Header("Stat Multiplier (Optional)")]
        [Tooltip("If set, multiplies the base magnitude by the target's stat value.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        [Header("Apply To Target")] public Target applyTo = Target.Target;
        public EntityLinkSchema applyToLink;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        [Tooltip("AllContacts applies once per contacting collider; FirstPerRoot applies once per enemy (root).")]
        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.AllContacts;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            PhysicsForceClipBaking.Bake(ref commands, context, PhysicsTriggerForceType.Directional, triggerState, mode,
                magnitude, direction, PhysicsTriggerPositionMode.MatchSelf, PhysicsTriggerFalloffCurve.None, 0f, 0f,
                strengthStat, readStatFrom, readStatLink, applyTo, applyToLink, ignoreTarget, requireLinks, hitMode);
            base.Bake(clipEntity, context);
        }
    }
}
