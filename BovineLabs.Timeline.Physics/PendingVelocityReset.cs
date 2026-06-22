using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct PendingVelocityReset : IComponentData, IEnableableComponent
    {
        public VelocityResetFlags Flags;
    }
}