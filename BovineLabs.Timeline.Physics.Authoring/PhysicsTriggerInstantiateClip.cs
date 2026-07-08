using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Nerve.Authoring.ObjectManagement;
using BovineLabs.Nerve.PhysicsStates;
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
    public sealed class PhysicsTriggerInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema assignParentLink;

        public ObjectDefinition objectDefinition;
        public StatefulEventState triggerState = StatefulEventState.Enter;

        public PhysicsTriggerPositionMode positionMode = PhysicsTriggerPositionMode.MatchContactPoint;
        public Vector3 positionOffset;
        public Target positionOffsetSpace = Target.Self;

        public PhysicsTriggerRotationMode rotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;
        public Vector3 rotationOffset;

        public Target assignParent = Target.None;

        [Header("Target Override")]
        [Tooltip("Resolves link on collided entity. Assigns to spawned entity Targets.Target.")]
        public EntityLinkSchema targetLinkOverride;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        [Tooltip(
            "AllContacts spawns once per contacting collider; FirstPerRoot spawns once per enemy (resolved root).")]
        public PhysicsTriggerHitMode hitMode = PhysicsTriggerHitMode.AllContacts;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            if (objectDefinition == null)
            {
                Debug.LogError($"{nameof(PhysicsTriggerInstantiateClip)} '{name}' needs {nameof(objectDefinition)}.");
                return;
            }

            context.Baker.DependsOn(objectDefinition);

            if (targetLinkOverride == null ||
                !EntityLinkAuthoringUtility.TryGetKey(targetLinkOverride, out var targetKey)) targetKey = 0;

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsTriggerInstantiateBuilder
            {
                InstantiateData = new PhysicsTriggerInstantiateData
                {
                    ObjectId = objectDefinition,
                    EventState = triggerState,
                    PositionMode = positionMode,
                    PositionOffset = positionOffset,
                    PositionOffsetSpace = positionOffsetSpace,
                    RotationMode = rotationMode,
                    RotationOffsetEuler = math.radians(rotationOffset),
                    AssignParent = EntityLinkAuthoringUtility.BakeRef(context.Baker, assignParentLink, assignParent),
                    TargetLinkKey = targetKey
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