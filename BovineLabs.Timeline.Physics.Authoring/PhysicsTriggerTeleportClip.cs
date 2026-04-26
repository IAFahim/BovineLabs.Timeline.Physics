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
    public class PhysicsTriggerTeleportClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Teleport Configuration")] public StatefulEventState TriggerState = StatefulEventState.Enter;
        [Tooltip("Who should be teleported? Target = The Entity colliding with us.")]
        public Target EntityToMove = Target.Owner;[Tooltip("If true, removes all momentum from the teleported entity so it doesn't fly away.")]
        public bool ResetPhysicsVelocity = true;

        [Header("Destination")]
        public PhysicsTriggerPositionMode PositionMode = PhysicsTriggerPositionMode.MatchContactPoint;
        public Vector3 PositionOffset = Vector3.zero;
        public Target PositionOffsetSpace = Target.Self;

        [Header("Facing Direction")]
        public PhysicsTriggerRotationMode RotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;
        public Vector3 RotationOffset = Vector3.zero;

        [Header("Hierarchy")] 
        public Target assignParent = Target.None;
        [Tooltip("If set, overrides Target and searches the collision hierarchy for this exact bone/link.")]
        public SourceSchema assignParentLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
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
                AssignParentLinkId = assignParentLink != null ? assignParentLink.Id : (byte)0
            });

            base.Bake(clipEntity, context);
        }
    }
}