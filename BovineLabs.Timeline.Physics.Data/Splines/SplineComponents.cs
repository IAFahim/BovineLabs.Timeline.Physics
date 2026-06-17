using BovineLabs.Core.Collections;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    ///     The stable route key a baked spline carries (assigned from its <c>SplineSchema</c>). Decouples
    ///     "which path" from any scene reference: a Timeline clip stores only this ushort (an asset→asset bake,
    ///     which serializes safely) and resolves the geometry at runtime via <see cref="SplineRegistry" />.
    /// </summary>
    public struct SplineKey : IComponentData
    {
        public ushort Value;
    }

    /// <summary>The baked, world-space spline geometry as a Burst-ready ECS blob (built from a Unity SplineContainer).</summary>
    public struct SplineBlob : IComponentData
    {
        public BlobAssetReference<BlobSpline> Value;
    }

    /// <summary>
    ///     Singleton: spline key → baked blob. Rebuilt (cheaply) by <c>SplineRegistrySystem</c> from every
    ///     entity carrying <see cref="SplineKey" /> + <see cref="SplineBlob" />, and read by any system that needs
    ///     to evaluate a path by key without holding a scene reference.
    /// </summary>
    public struct SplineRegistry : IComponentData
    {
        public NativeHashMap<ushort, BlobAssetReference<BlobSpline>> Map;
    }
}
