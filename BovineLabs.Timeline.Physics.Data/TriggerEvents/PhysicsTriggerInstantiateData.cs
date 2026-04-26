using BovineLabs.Core.ObjectManagement;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerInstantiateData : IComponentData
    {
        public ObjectId ObjectId;
        public StatefulEventState EventState;

        public PhysicsTriggerPositionMode PositionMode;
        public float3 PositionOffset;
        public Target PositionOffsetSpace;

        public PhysicsTriggerRotationMode RotationMode;
        public float3 RotationOffsetEuler;

        public Target AssignParent;
        public byte AssignParentLinkId;
    }
}