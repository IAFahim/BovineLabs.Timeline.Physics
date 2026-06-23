using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    [BurstCompile]
    public static class BreakImpulseKernel
    {
        public static float3 ComputeDeltaV(PhysicsBreakMode mode, quaternion selfRot, float3 velocity, float speed,
            float elevation, float azimuth, float restitution)
        {
            if (mode == PhysicsBreakMode.Brake)
                return -(1f + restitution) * velocity;

            math.sincos(elevation, out var se, out var ce);
            math.sincos(azimuth, out var sa, out var ca);
            var localDir = new float3(sa * ce, se, ca * ce);
            var target = math.rotate(selfRot, localDir) * (restitution * speed);
            return target - velocity;
        }
    }
}
