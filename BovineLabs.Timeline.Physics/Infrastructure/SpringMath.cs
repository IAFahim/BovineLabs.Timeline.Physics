using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    public static class SpringMath
    {
        private const float Ln2 = 0.69314718056f;
        private const float Epsilon = 1e-5f;

        public static float HalflifeToDamping(float halflife)
        {
            return 4f * Ln2 / math.max(halflife, Epsilon);
        }

        public static void CriticalSpring(
            in float3 position,
            in float3 velocity,
            in float3 goalPosition,
            in float3 goalVelocity,
            float halflife,
            float deltaTime,
            out float3 nextPosition,
            out float3 nextVelocity)
        {
            var y = HalflifeToDamping(halflife) * 0.5f;
            var displacement = position - goalPosition;
            var relativeVelocity = velocity - goalVelocity;
            var coupled = relativeVelocity + displacement * y;
            var decay = math.exp(-y * deltaTime);

            nextPosition = decay * (displacement + coupled * deltaTime) + goalPosition + goalVelocity * deltaTime;
            nextVelocity = decay * (relativeVelocity - coupled * y * deltaTime) + goalVelocity;
        }

        public static void CriticalSpringRotation(
            in quaternion rotation,
            in float3 angularVelocity,
            in quaternion goalRotation,
            float halflife,
            float deltaTime,
            out quaternion nextRotation,
            out float3 nextAngularVelocity)
        {
            var y = HalflifeToDamping(halflife) * 0.5f;
            var displacement = Log(math.mul(rotation, math.conjugate(goalRotation)));
            var coupled = angularVelocity + displacement * y;
            var decay = math.exp(-y * deltaTime);

            var nextDisplacement = decay * (displacement + coupled * deltaTime);
            nextAngularVelocity = decay * (angularVelocity - coupled * y * deltaTime);
            nextRotation = math.normalize(math.mul(Exp(nextDisplacement), goalRotation));
        }

        public static float3 Log(in quaternion q)
        {
            var v = q.value;
            var positive = math.select(v, -v, v.w < 0f);
            var sinHalf = math.sqrt(math.max(0f, 1f - positive.w * positive.w));

            if (sinHalf < Epsilon)
                return 2f * positive.xyz;

            var angle = 2f * math.acos(math.clamp(positive.w, -1f, 1f));
            return positive.xyz / sinHalf * angle;
        }

        public static quaternion Exp(in float3 rotationVector)
        {
            var angle = math.length(rotationVector);
            if (angle < Epsilon)
                return math.normalize(new quaternion(rotationVector.x * 0.5f, rotationVector.y * 0.5f,
                    rotationVector.z * 0.5f, 1f));

            return quaternion.AxisAngle(rotationVector / angle, angle);
        }
    }
}