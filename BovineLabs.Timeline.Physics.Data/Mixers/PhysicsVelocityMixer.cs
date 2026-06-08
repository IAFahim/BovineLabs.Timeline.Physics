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
            var aWins = (byte)a.Mode < (byte)b.Mode ||
                        ((byte)a.Mode == (byte)b.Mode && (byte)a.Space <= (byte)b.Space);
            var dominant = aWins ? a : b;

            return new PhysicsVelocityData
            {
                Mode = dominant.Mode,
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = dominant.Space,
                Strength = dominant.Strength
            };
        }
    }
}