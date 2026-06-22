using Unity.Collections.LowLevel.Unsafe;

namespace BovineLabs.Timeline.Physics.Data.Kernels
{
    public readonly struct DiscreteMixer<T> : IMixer<T>
        where T : unmanaged
    {
        public T Lerp(in T a, in T b, in float s)
        {
            var aDefault = IsDefault(a);
            var bDefault = IsDefault(b);
            return bDefault || (!aDefault && s < 0.5f) ? a : b;
        }

        public T Add(in T a, in T b)
        {
            return b;
        }

        private static unsafe bool IsDefault(in T v)
        {
            var value = v;
            var zero = default(T);
            return UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref value), UnsafeUtility.AddressOf(ref zero),
                UnsafeUtility.SizeOf<T>()) == 0;
        }
    }
}