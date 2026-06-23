using Unity.Burst;
using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    [BurstCompile]
    public static class VelocityClampKernel
    {
        public static PhysicsVelocity Clamp(PhysicsVelocity velocity, float maxLinearSpeed, float maxAngularSpeed)
        {
            if (maxLinearSpeed >= 0f)
            {
                var linSq = math.lengthsq(velocity.Linear);
                if (linSq > maxLinearSpeed * maxLinearSpeed)
                    velocity.Linear = math.normalize(velocity.Linear) * maxLinearSpeed;
            }

            if (maxAngularSpeed >= 0f)
            {
                var angSq = math.lengthsq(velocity.Angular);
                if (angSq > maxAngularSpeed * maxAngularSpeed)
                    velocity.Angular = math.normalize(velocity.Angular) * maxAngularSpeed;
            }

            return velocity;
        }
    }
}
