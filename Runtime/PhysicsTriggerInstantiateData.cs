using BovineLabs.Core.PhysicsStates;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerInstantiateData : IComponentData
    {
        public Entity Prefab;
        public StatefulEventState EventState;
        public bool SnapToTransform;
    }
}