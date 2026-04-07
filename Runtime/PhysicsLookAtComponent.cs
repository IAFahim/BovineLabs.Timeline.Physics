using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsLookAtComponent : IComponentData
    {
        public float ProportionalGain;
        public float IntegralGain;
        public float DerivativeGain;
        public float MaxAngularVelocity;

        public float3 Integral;
        public float3 PreviousError;
    }
}
