using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Smear
{
    // This attribute tells DOTS to push this value to the shader property "_Velocity"
    [MaterialProperty("_Velocity")]
    public struct SmearVelocity : IComponentData
    {
        public float4 Value;
    }
}
