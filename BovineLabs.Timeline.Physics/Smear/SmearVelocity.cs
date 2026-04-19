using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace BovineLabs.Timeline.Physics.Smear
{
    [MaterialProperty("_Velocity")]
    public struct SmearVelocity : IComponentData
    {
        public float4 Value;
    }
}