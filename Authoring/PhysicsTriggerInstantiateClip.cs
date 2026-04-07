using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsTriggerInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Spawn Configuration")]
        public GameObject Prefab;
        public StatefulEventState TriggerState = StatefulEventState.Enter;

        [Header("Position")]
        public InstantiatePositionMode PositionMode = InstantiatePositionMode.MatchContactPoint;
        public Vector3 PositionOffset = Vector3.zero;
        public bool IsPositionOffsetLocal = true;

        [Header("Rotation")]
        public InstantiateRotationMode RotationMode = InstantiateRotationMode.AlignToContactNormal;
        [Tooltip("Euler angles to offset the final rotation (e.g., (0, 180, 0) to face inward)")]
        public Vector3 RotationOffset = Vector3.zero;

        [Header("Hierarchy")]
        public InstantiateParentMode ParentMode = InstantiateParentMode.None;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsTriggerInstantiateData
            {
                Prefab = context.Baker.GetEntity(Prefab, TransformUsageFlags.None),
                EventState = TriggerState,
                PositionMode = PositionMode,
                PositionOffset = PositionOffset,
                IsPositionOffsetLocal = IsPositionOffsetLocal,
                RotationMode = RotationMode,
                RotationOffsetEuler = math.radians(RotationOffset),
                ParentMode = ParentMode
            });

            base.Bake(clipEntity, context);
        }
    }
}