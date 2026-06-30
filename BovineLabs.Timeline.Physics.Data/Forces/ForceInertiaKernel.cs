using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Data.Forces
{
    [BurstCompile]
    public static class ForceInertiaKernel
    {
        public static PhysicsVelocity ApplyForcesToVelocity(in PhysicsVelocity velocity, float3 totalLinear,
            float3 totalAngular, in PhysicsMass mass, in LocalToWorld transform)
        {
            var result = velocity;
            result.Linear += totalLinear * mass.InverseMass;

            var rotation = new quaternion(math.orthonormalize(new float3x3(transform.Value)));
            var inertiaRot = math.mul(rotation, mass.Transform.rot);
            var localAngular = math.rotate(math.inverse(inertiaRot), totalAngular);
            result.Angular += localAngular * mass.InverseInertia;

            return result;
        }
    }
}
