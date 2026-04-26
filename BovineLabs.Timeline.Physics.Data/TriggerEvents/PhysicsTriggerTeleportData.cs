using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerTeleportData : IComponentData
    {
        public StatefulEventState EventState;

        public Target EntityToMove;

        public PhysicsTriggerPositionMode PositionMode;
        public float3 PositionOffset;
        public Target PositionOffsetSpace;

        public PhysicsTriggerRotationMode RotationMode;
        public float3 RotationOffsetEuler;

        public bool ResetVelocity;

        public Target AssignParent;
        public ushort AssignParentLinkKey;
    }
}