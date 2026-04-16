using BovineLabs.Core.PhysicsStates;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct OnClipActiveStatefulInstantiateTag : IComponentData
    {
    }

    public struct StatefulEventStateConfig : IComponentData
    {
        public StatefulEventState Value;
    }
}