using System;
using BovineLabs.Core.Authoring.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        public ObjectDefinition objectDefinition;
        public StatefulEventState triggerState = StatefulEventState.Enter;

        public PhysicsTriggerPositionMode positionMode = PhysicsTriggerPositionMode.MatchContactPoint;
        public Vector3 positionOffset;
        public Target positionOffsetSpace = Target.Self;

        public PhysicsTriggerRotationMode rotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;
        public Vector3 rotationOffset;

        public Target assignParent = Target.None;
        public EntityLinkSchema assignParentLink;

        [Header("Target Override")]
        [Tooltip("Resolves link on collided entity. Assigns to spawned entity Targets.Target.")]
        public EntityLinkSchema targetLinkOverride;

        [Header("Filtering")]
        [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (objectDefinition == null)
            {
                Debug.LogError($"{nameof(PhysicsTriggerInstantiateClip)} '{name}' needs {nameof(objectDefinition)}.");
                return;
            }

            context.Baker.DependsOn(objectDefinition);

            if (assignParentLink == null || !EntityLinkAuthoringUtility.TryGetKey(assignParentLink, out var parentKey)) parentKey = 0;
            if (targetLinkOverride == null || !EntityLinkAuthoringUtility.TryGetKey(targetLinkOverride, out var targetKey)) targetKey = 0;

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerInstantiateData
            {
                ObjectId = objectDefinition,
                EventState = triggerState,
                PositionMode = positionMode,
                PositionOffset = positionOffset,
                PositionOffsetSpace = positionOffsetSpace,
                RotationMode = rotationMode,
                RotationOffsetEuler = math.radians(rotationOffset),
                AssignParent = assignParent,
                AssignParentLinkKey = parentKey,
                TargetLinkKey = targetKey
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