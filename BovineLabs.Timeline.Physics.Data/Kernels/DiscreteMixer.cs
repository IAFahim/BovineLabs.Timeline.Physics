namespace BovineLabs.Timeline.Physics.Data.Kernels
{
    public readonly struct DiscreteMixer<T> : IMixer<T>
        where T : unmanaged
    {
        public T Lerp(in T a, in T b, in float s)
        {
            return s >= 0.5f ? b : a;
        }

        public T Add(in T a, in T b)
        {
            return b;
        }
    }
}