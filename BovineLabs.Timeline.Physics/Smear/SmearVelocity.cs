using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Smear
{
    [MaterialProperty("_Velocity")]
    public struct SmearVelocity : IComponentData
    {
        public float4 Value;
    }
}
