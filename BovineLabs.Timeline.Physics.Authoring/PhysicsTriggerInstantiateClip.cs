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

            if (!EntityLinkAuthoringUtility.TryGetKey(assignParentLink, out var linkKey)) linkKey = 0;

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
                AssignParentLinkKey = linkKey
            });

            base.Bake(clipEntity, context);
        }
    }
}