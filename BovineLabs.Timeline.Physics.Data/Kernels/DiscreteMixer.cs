using Unity.Collections.LowLevel.Unsafe;

namespace BovineLabs.Timeline.Physics.Data.Kernels
{
    public readonly struct DiscreteMixer<T> : IMixer<T>
        where T : unmanaged
    {
        public T Lerp(in T a, in T b, in float s)
        {
            // A discrete override is binary, never interpolated. JobHelpers.Blend injects the synthetic
            // default-fill - default(T) - as a phantom operand to fade an under-weighted clip toward zero.
            // Operand order is NOT guaranteed: the 2-value blend passes (authored, default) but the 4-value
            // outer blend passes (low-priority pair, high-priority pair), so the default can land in either
            // slot. Prefer the non-default (authored) operand; only when both are authored does weight decide
            // (a carries weight 1 - s, b carries s, so s < 0.5f keeps a). This never resolves to the phantom
            // default, which previously zeroed the override mid-ramp and tunnelled the body through everything.
            var aDefault = IsDefault(a);
            var bDefault = IsDefault(b);
            return (bDefault || (!aDefault && s < 0.5f)) ? a : b;
        }

        public T Add(in T a, in T b)
        {
            return b;
        }

        private static unsafe bool IsDefault(in T v)
        {
            var value = v;
            var zero = default(T);
            return UnsafeUtility.MemCmp(UnsafeUtility.AddressOf(ref value), UnsafeUtility.AddressOf(ref zero), UnsafeUtility.SizeOf<T>()) == 0;
        }
    }
}