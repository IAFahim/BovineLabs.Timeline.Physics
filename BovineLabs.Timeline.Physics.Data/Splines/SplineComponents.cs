using BovineLabs.Core.Collections;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct SplineKey : IComponentData
    {
        public ushort Value;
    }

    public struct SplineBlob : IComponentData
    {
        public BlobAssetReference<BlobSpline> Value;
    }

    public struct SplineRegistry : IComponentData
    {
        public NativeHashMap<ushort, BlobAssetReference<BlobSpline>> Map;
    }
}