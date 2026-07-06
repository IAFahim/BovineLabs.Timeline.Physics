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
                Strength = discrete.Strength,

                // Propagate presence so a mixed intermediate isn't mistaken for an empty slot in a multi-fold blend
                // (3+ overlapping clips) — otherwise the newest clip's discrete fields would win at any weight.
                Present = (byte)(a.Present | b.Present)
            };
        }

        private static bool IsDefault(in PhysicsVelocityData v)
        {
            // Explicit identity, not structural: an authored clip carries Present=1 (set at bake), so a legitimate
            // zero-velocity SetContinuous "stop" clip is no longer mistaken for the injected empty-slot default.
            return v.Present == 0;
        }

        public PhysicsVelocityData Add(in PhysicsVelocityData a, in PhysicsVelocityData b)
        {
            // The additive fold calls Add(defaultValue, result) with an empty defaultValue in slot a; guard so the
            // empty slot never wins the discrete fields via the Mode/Space tie-break.
            var aDefault = IsDefault(a);
            var bDefault = IsDefault(b);
            var aWins = bDefault ||
                        (!aDefault && ((byte)a.Mode < (byte)b.Mode ||
                                       ((byte)a.Mode == (byte)b.Mode && (byte)a.Space >= (byte)b.Space)));
            var dominant = aWins ? a : b;

            return new PhysicsVelocityData
            {
                Mode = dominant.Mode,
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Space = dominant.Space,
                ResetVelocityOnFire = dominant.ResetVelocityOnFire,
                Strength = dominant.Strength,
                Present = (byte)(a.Present | b.Present)
            };
        }
    }
}