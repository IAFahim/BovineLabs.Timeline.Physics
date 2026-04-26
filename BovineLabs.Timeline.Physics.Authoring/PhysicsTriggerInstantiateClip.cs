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
            if (this.objectDefinition == null)
            {
                Debug.LogError($"{nameof(PhysicsTriggerInstantiateClip)} '{this.name}' needs {nameof(this.objectDefinition)}.");
                return;
            }

            context.Baker.DependsOn(this.objectDefinition);

            if (!EntityLinkAuthoringUtility.TryGetKey(this.assignParentLink, out ushort linkKey))
            {
                linkKey = 0;
            }

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerInstantiateData
            {
                ObjectId = this.objectDefinition,
                EventState = this.triggerState,
                PositionMode = this.positionMode,
                PositionOffset = this.positionOffset,
                PositionOffsetSpace = this.positionOffsetSpace,
                RotationMode = this.rotationMode,
                RotationOffsetEuler = math.radians(this.rotationOffset),
                AssignParent = this.assignParent,
                AssignParentLinkKey = linkKey
            });

            base.Bake(clipEntity, context);
        }
    }
}
