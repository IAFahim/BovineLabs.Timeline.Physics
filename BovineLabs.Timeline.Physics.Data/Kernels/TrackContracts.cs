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
}