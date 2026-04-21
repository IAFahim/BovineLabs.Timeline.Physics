using BovineLabs.Core.Authoring.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsTriggerInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Spawn")] public ObjectDefinition objectDefinition;

        public StatefulEventState triggerState = StatefulEventState.Enter;

        [Header("Position")]
        public PhysicsTriggerPositionMode positionMode = PhysicsTriggerPositionMode.MatchContactPoint;

        public Vector3 positionOffset = Vector3.zero;
        public Target positionOffsetSpace = Target.Self;

        [Header("Rotation")]
        public PhysicsTriggerRotationMode rotationMode = PhysicsTriggerRotationMode.AlignToContactNormal;

        [Tooltip("Euler angles to offset the final rotation (e.g., (0, 180, 0) to face inward)")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Hierarchy")] 
        public Target assignParent = Target.None;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsTriggerInstantiateData
            {
                ObjectId = objectDefinition,
                EventState = triggerState,
                PositionMode = positionMode,
                PositionOffset = positionOffset,
                PositionOffsetSpace = positionOffsetSpace,
                RotationMode = rotationMode,
                RotationOffsetEuler = math.radians(rotationOffset),
                AssignParent = assignParent
            });

            base.Bake(clipEntity, context);
        }
    }
}