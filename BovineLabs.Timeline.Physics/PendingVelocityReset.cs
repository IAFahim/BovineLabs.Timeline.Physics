using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    ///     Request to zero the body's velocity before pending forces and velocities are drained.
    ///     Producers OR their flags in and enable the component at fire time; the force accumulator
    ///     consumes it exactly once per request (zero masked axes, clear flags, disable), so a dash
    ///     impulse always lands on a clean slate and repeats travel identical distances.
    /// </summary>
    public struct PendingVelocityReset : IComponentData, IEnableableComponent
    {
        public VelocityResetFlags Flags;
    }
}
