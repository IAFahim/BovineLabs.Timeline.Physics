using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsTriggerTeleportClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Teleport Configuration")] public StatefulEventState TriggerState = StatefulEventState.Enter;

        [Tooltip("Who should be teleported?")]
        public PhysicsTriggerTargetMode EntityToMove = PhysicsTriggerTargetMode.ReactionOwner;

        [Tooltip("If true, removes all momentum from the teleported entity so it doesn't fly away.")]
        public bool ResetPhysicsVelocity = true;

        [Header("Destination")]
        public PhysicsTriggerPositionMode PositionMode = PhysicsTriggerPositionMode.MatchContactPoint;

        public Vector3 PositionOffset = Vector3.zero;
        public bool IsPositionOffsetLocal = true;

        [Header("Facing Direction")]
        public PhysicsTriggerRotationMode RotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;

        public Vector3 RotationOffset = Vector3.zero;

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
                IsPositionOffsetLocal = IsPositionOffsetLocal,
                RotationMode = RotationMode,
                RotationOffsetEuler = math.radians(RotationOffset),
                ResetVelocity = ResetPhysicsVelocity
            });

            base.Bake(clipEntity, context);
        }
    }
}
