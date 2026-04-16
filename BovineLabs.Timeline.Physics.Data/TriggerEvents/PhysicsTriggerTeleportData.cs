using BovineLabs.Core.PhysicsStates;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerTeleportData : IComponentData
    {
        public StatefulEventState EventState;

        public PhysicsTriggerTargetMode EntityToMove;

        public PhysicsTriggerPositionMode PositionMode;
        public float3 PositionOffset;
        public bool IsPositionOffsetLocal;

        public PhysicsTriggerRotationMode RotationMode;
        public float3 RotationOffsetEuler;

        public bool ResetVelocity;
    }
}