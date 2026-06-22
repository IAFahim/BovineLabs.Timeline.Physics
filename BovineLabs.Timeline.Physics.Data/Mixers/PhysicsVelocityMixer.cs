using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Data.Mixers
{
    public readonly struct PhysicsVelocityMixer : IMixer<PhysicsVelocityData>
    {
        public PhysicsVelocityData Lerp(in PhysicsVelocityData a, in PhysicsVelocityData b, in float s)
        {
            // JobHelpers.Blend injects default(PhysicsVelocityData) as a phantom operand to fade an
            // under-weighted clip toward zero. Its Mode is SetContinuous (enum 0), so a discrete
            // 's < 0.5f' pick would let that phantom flip an authored SetInstant clip to SetContinuous
            // while the real clip weight is below 0.5 - breaking fire-once semantics. The discrete
            // fields therefore stick to the non-default operand; only when both are authored do we
            // fall back to the higher-weight operand (s < 0.5f selects a, which carries weight 1 - s).
            var aDefault = IsDefault(a);
            var bDefault = IsDefault(b);
            var discrete = (bDefault || (!aDefault && s < 0.5f)) ? a : b;

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
