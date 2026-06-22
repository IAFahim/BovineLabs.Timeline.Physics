using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Data.Mixers
{
    public readonly struct PhysicsVelocityMixer : IMixer<PhysicsVelocityData>
    {
        public PhysicsVelocityData Lerp(in PhysicsVelocityData a, in PhysicsVelocityData b, in float s)
        {
            var aDefault = IsDefault(a);
            var bDefault = IsDefault(b);
            var discrete = bDefault || (!aDefault && s < 0.5f) ? a : b;

            return new PhysicsVelocityData
            {
                Mode = discrete.Mode,
                Linear = math.lerp(a.Linear, b.Linear, s),
                Angular = math.lerp(a.Angular, b.Angular, s),
                Space = discrete.Space,
                ResetVelocityOnFire = discrete.ResetVelocityOnFire,
                Strength = discrete.Strength
            };
        }

        private static bool IsDefault(in PhysicsVelocityData v)
        {
            return v.Mode == PhysicsVelocityMode.SetContinuous &&
                   math.all(v.Linear == float3.zero) &&
                   math.all(v.Angular == float3.zero);
        }

        public PhysicsVelocityData Add(in PhysicsVelocityData a, in PhysicsVelocityData b)
        {
            var aWins = (byte)a.Mode < (byte)b.Mode ||
                        ((byte)a.Mode == (byte)b.Mode && (byte)a.Space >= (byte)b.Space);
            var dominant = aWins ? a : b;

            return new PhysicsVelocityData
            {
                Mode = dominant.Mode,
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = dominant.Space,
                ResetVelocityOnFire = dominant.ResetVelocityOnFire,
                Strength = dominant.Strength
            };
        }
    }
}