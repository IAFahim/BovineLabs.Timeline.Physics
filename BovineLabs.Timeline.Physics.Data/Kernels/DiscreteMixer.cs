namespace BovineLabs.Timeline.Physics.Data.Kernel
{
    public readonly struct DiscreteMixer<T> : IMixer<T>
        where T : unmanaged
    {
        public T Lerp(in T a, in T b, in float s) => s >= 0.5f ? b : a;
        public T Add(in T a, in T b) => b;
    }
}