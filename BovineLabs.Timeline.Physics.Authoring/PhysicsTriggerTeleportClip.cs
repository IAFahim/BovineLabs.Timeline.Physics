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
    public sealed class PhysicsTriggerTeleportClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState TriggerState = StatefulEventState.Enter;
        public Target EntityToMove = Target.Owner;
        public bool ResetPhysicsVelocity = true;

        public PhysicsTriggerPositionMode PositionMode = PhysicsTriggerPositionMode.MatchContactPoint;
        public Vector3 PositionOffset;
        public Target PositionOffsetSpace = Target.Self;

        public PhysicsTriggerRotationMode RotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;
        public Vector3 RotationOffset;

        public Target assignParent = Target.None;
        public EntityLinkSchema assignParentLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            if (!EntityLinkAuthoringUtility.TryGetKey(assignParentLink, out var linkKey)) linkKey = 0;

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerTeleportData
            {
                EventState = TriggerState,
                EntityToMove = EntityToMove,
                PositionMode = PositionMode,
                PositionOffset = PositionOffset,
                PositionOffsetSpace = PositionOffsetSpace,
                RotationMode = RotationMode,
                RotationOffsetEuler = math.radians(RotationOffset),
                ResetVelocity = ResetPhysicsVelocity,
                AssignParent = assignParent,
                AssignParentLinkKey = linkKey
            });

            base.Bake(clipEntity, context);
        }
    }
}