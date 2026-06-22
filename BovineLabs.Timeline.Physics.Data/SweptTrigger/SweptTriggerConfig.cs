using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    public struct SweptTriggerConfig : IComponentData
    {
        public BlobAssetReference<Collider> Collider;

        public int SubSteps;

        public float TipRadius;

        public float Thickness;

        public float3 DebugCenter;

        public float3 DebugExtents;
    }
}