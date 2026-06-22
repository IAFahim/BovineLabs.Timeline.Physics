using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct SweptTriggerState : IComponentData
    {
        public float3 PrevPosition;
        public quaternion PrevRotation;

        public byte WasActive;
    }
}