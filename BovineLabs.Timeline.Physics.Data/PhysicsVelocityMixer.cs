using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public readonly struct PhysicsVelocityMixer : IMixer<PhysicsVelocityData>
    {
        public PhysicsVelocityData Lerp(in PhysicsVelocityData a, in PhysicsVelocityData b, in float s)
        {
            return new PhysicsVelocityData
            {
                Linear = math.lerp(a.Linear, b.Linear, s),
                Angular = math.lerp(a.Angular, b.Angular, s),
                Space = s < 0.5f ? a.Space : b.Space
            };
        }

        public PhysicsVelocityData Add(in PhysicsVelocityData a, in PhysicsVelocityData b)
        {
            return new PhysicsVelocityData
            {
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = a.Space
            };
        }
    }
}