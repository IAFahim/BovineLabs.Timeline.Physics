namespace BovineLabs.Timeline.Physics.Data.Kernels
{
    using Unity.Entities;
    
    public interface IActive<TData> : IComponentData, IEnableableComponent where TData : unmanaged
    {
        TData Config { get; set; }
    }

    public interface IPreparable
    {
        void ResetToAuthored();
    }
}