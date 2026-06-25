using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Package-owned, fixed-step clip activation gate for physics trigger clips.
    /// Core <c>ClipActive</c>/<c>ClipActivePrevious</c> are point-sampled in the variable-rate
    /// BeforeTransformSystemGroup, so inside FixedStepSimulationSystemGroup they are stale by a frame and
    /// <c>ClipActive == ClipActivePrevious</c> — the activation edge is invisible and short windows are missed.
    /// <see cref="BovineLabs.Timeline.Physics.Infrastructure.PhysicsClipGateSystem"/> recomputes this from the
    /// clip timer interval in fixed-step time. Enabled state == crossing-aware active this fixed tick.
    /// </summary>
    public struct PhysicsClipGate : IComponentData, IEnableableComponent
    {
        /// <summary> 1 on the first fixed tick this clip's window is (crossing-aware) active. </summary>
        public byte FirstFrame;

        /// <summary> 1 on the active fixed tick the clip timer crosses its end. </summary>
        public byte LastFrame;

        /// <summary> Producer-owned previous active state, in fixed-step time (drives the FirstFrame edge). </summary>
        public byte WasActive;
    }
}
