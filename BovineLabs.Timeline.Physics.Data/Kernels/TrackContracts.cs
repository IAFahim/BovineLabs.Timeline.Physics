using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Data.Kernels
{
    public interface IActive<TData> : IComponentData, IEnableableComponent where TData : unmanaged
    {
        TData Config { get; set; }
    }

    public interface IPreparable
    {
        void ResetToAuthored();
    }

    /// <summary>
    /// Track state that accumulates render-rate clip-active time for a fixed-step consumer
    /// (the ElapsedTime/AppliedTime determinism bridge — see PhysicsForceState).
    /// </summary>
    public interface IElapsedTimeState
    {
        float ElapsedTime { get; set; }
    }

    /// <summary>
    /// A capture-restore track state that may be holding a pending exit restore (its OnExit has not run yet, so its
    /// captured original is still owed back to the body). The SpanStart re-arm reset skips such states so a clip gap
    /// with zero fixed ticks in it (common at high fps vs a 50/60Hz fixed step) never wipes the captured original
    /// before the eventual real exit restores it. Implemented by returning the state's <c>Fired</c> flag.
    /// </summary>
    public interface IRestorableState
    {
        bool RestorePending { get; }
    }

    /// <summary>
    /// The clock-domain-crossing marker for a fire-once / continuous-motion latch (Force, Velocity, PID, Teleport):
    /// when a clip stops driving the body's <c>ActiveX</c> latch, the render-rate stale-disable set this instead of
    /// dropping the latch, so the fixed clock is guaranteed at least one apply tick to service it before it is
    /// disabled. See <see cref="IDrainableLatchState{TData}"/>.
    /// </summary>
    public interface IOrphanedLatch
    {
        /// <summary>
        /// 1 while the latch is lingering enabled after its last driving clip ended, waiting for the fixed-step apply
        /// to observe it. Cleared on re-arm and by the fixed-step drain-finalize once the apply has serviced it.
        /// </summary>
        bool Orphaned { get; set; }
    }

    /// <summary>
    /// A fire-once / continuous-motion latch state that participates in the fixed-step drain gate.
    /// <para>
    /// The render side blends clips into the body's <c>ActiveX</c> latch and enables it through a next-frame
    /// (<c>BeginSimulation</c>) ECB, so the enable window is delayed by one render frame; the disable is immediate.
    /// A clip whose active window straddles no fixed tick inside that delayed window would therefore be dropped
    /// (impulse never fires, teleport never happens) and a continuous force/velocity would lose its unconsumed
    /// <c>ElapsedTime - AppliedTime</c> tail. To close that render-rate to fixed-step seam, the stale-disable does
    /// not drop an <em>undrained</em> latch: it lingers it enabled (<see cref="IOrphanedLatch.Orphaned"/>) so the
    /// fixed clock gets one apply tick to fire/drain it, after which the drain-finalize disables it.
    /// </para>
    /// <see cref="IsDrained"/> lets the disable skip the linger when the effect has already been delivered this
    /// activation (the common case), preserving zero deactivation latency there.
    /// </summary>
    public interface IDrainableLatchState<TData> : IOrphanedLatch
        where TData : unmanaged
    {
        /// <summary>
        /// True once this activation's fixed-step effect has been fully delivered for <paramref name="config"/>
        /// (impulse fired, or a continuous integrator has caught <c>AppliedTime</c> up to <c>ElapsedTime</c>), so the
        /// latch can be disabled immediately without a linger tick.
        /// </summary>
        bool IsDrained(in TData config);
    }
}