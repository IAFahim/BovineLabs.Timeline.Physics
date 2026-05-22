using Unity.Entities;
using Unity.Physics.Systems;

namespace BovineLabs.Timeline.Physics
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial class PhysicsProducerGroup : ComponentSystemGroup
    {
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial class PhysicsModifierGroup : ComponentSystemGroup
    {
    }
}