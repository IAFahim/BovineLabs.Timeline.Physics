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
                Angular = math.lerp(a.Angular, b.Angular, s)
            };
        }

        public PhysicsVelocityData Add(in PhysicsVelocityData a, in PhysicsVelocityData b)
        {
            return new PhysicsVelocityData
            {
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular
            };
        }
    }
}