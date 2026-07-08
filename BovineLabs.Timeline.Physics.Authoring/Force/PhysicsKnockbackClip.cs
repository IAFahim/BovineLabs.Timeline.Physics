using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Nerve.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Radial push/pull from an origin (knockback / implosion). One of the focused clips that replace the
    /// multi-mode PhysicsTriggerForceClip — bakes the same <see cref="PhysicsTriggerForceData"/> with
    /// ForceType = Radial, so the runtime is unchanged; only the authoring surface is focused.
    /// </summary>
    public sealed class PhysicsKnockbackClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Positive pulls the target IN (implosion), negative pushes it OUT (explosion/knockback).")]
        public float magnitude = -10f;

        [Header("Origin / Falloff")]
        public PhysicsTriggerPositionMode originMode = PhysicsTriggerPositionMode.MatchSelf;

        public PhysicsTriggerFalloffCurve falloffCurve = PhysicsTriggerFalloffCurve.None;
        [Min(0f)] public float falloffStartRadius;
        [Min(0f)] public float falloffEndRadius = 5f;

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
            // Knockback is a victim impulse — External so it survives the victim braking.
            PhysicsForceClipBaking.Bake(ref commands, context, PhysicsTriggerForceType.Radial, triggerState, mode,
                MotionChannel.External,
                magnitude, float3.zero, originMode, falloffCurve, falloffStartRadius, falloffEndRadius, strengthStat,
                readStatFrom, readStatLink, applyTo, applyToLink, ignoreTarget, requireLinks, hitMode);
            base.Bake(clipEntity, context);
        }
    }
}
