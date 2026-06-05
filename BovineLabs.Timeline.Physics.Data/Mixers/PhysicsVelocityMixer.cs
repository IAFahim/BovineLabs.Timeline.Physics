using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Data.Mixers
{
    public readonly struct PhysicsVelocityMixer : IMixer<PhysicsVelocityData>
    {
        public PhysicsVelocityData Lerp(in PhysicsVelocityData a, in PhysicsVelocityData b, in float s)
        {
            return new PhysicsVelocityData
            {
                Mode = s < 0.5f ? a.Mode : b.Mode,
                Linear = math.lerp(a.Linear, b.Linear, s),
                Angular = math.lerp(a.Angular, b.Angular, s),
                Space = s < 0.5f ? a.Space : b.Space,
                Strength = s < 0.5f ? a.Strength : b.Strength
            };
        }

        public PhysicsVelocityData Add(in PhysicsVelocityData a, in PhysicsVelocityData b)
        {
            // Note: If spaces differ, we still add the vectors but use the dominant 'a' space.
            // This is mathematically incorrect for different coordinate spaces (e.g. World + Local)
            // but preferable to completely dropping 'b' (lossy). A proper fix would require
            // access to LocalToWorld to convert 'b' into 'a's space during Add.
            return new PhysicsVelocityData
            {
                Mode = a.Mode,
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = a.Space,
                Strength = a.Strength
            };
        }
    }
}