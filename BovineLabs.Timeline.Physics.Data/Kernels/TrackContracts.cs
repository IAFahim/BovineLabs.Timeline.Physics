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
}