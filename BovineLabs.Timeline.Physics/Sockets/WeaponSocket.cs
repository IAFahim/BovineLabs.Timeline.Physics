using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Sockets
{
    public struct WeaponSocket : IComponentData, IEnableableComponent
    {
        public Entity Bone;
        public float3 LocalPosition;
        public quaternion LocalRotation;
    }
}