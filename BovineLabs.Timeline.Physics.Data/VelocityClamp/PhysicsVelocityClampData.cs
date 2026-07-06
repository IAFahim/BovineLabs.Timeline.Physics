using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsVelocityClampData : IComponentData
    {
        public float MaxLinearSpeed;
        public float MaxAngularSpeed;

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. The kernel
        // treats a max speed of 0 as the STRONGEST clamp (freeze) and only a negative value as "no clamp", so an
        // empty slot filled with 0 would freeze the body at partial weight — byte-identical to an authored freeze.
        // The mixer keys off this flag to relax an empty slot toward "no clamp" instead of toward the freeze at 0.
        public byte Present;
    }

    public struct PhysicsVelocityClampAnimated : IAnimatedComponent<PhysicsVelocityClampData>, IPreparable
    {
        public PhysicsVelocityClampData AuthoredData;
        [CreateProperty] public PhysicsVelocityClampData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveVelocityClamp : IActive<PhysicsVelocityClampData>
    {
        public PhysicsVelocityClampData Config { get; set; }
    }

    public struct PhysicsVelocityClampState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsVelocityClampMixer : IMixer<PhysicsVelocityClampData>
    {
        public PhysicsVelocityClampData Lerp(in PhysicsVelocityClampData a, in PhysicsVelocityClampData b, in float s)
        {
            var aEmpty = a.Present == 0;
            var bEmpty = b.Present == 0;

            // Both present (normal blend) or both empty (result stays empty): a straight lerp is correct either way.
            // For the empty case both maxes are 0, so the lerp is a no-op and Present propagates as 0.
            if (aEmpty == bEmpty)
            {
                return new PhysicsVelocityClampData
                {
                    MaxLinearSpeed = math.lerp(a.MaxLinearSpeed, b.MaxLinearSpeed, s),
                    MaxAngularSpeed = math.lerp(a.MaxAngularSpeed, b.MaxAngularSpeed, s),
                    Present = (byte)(a.Present | b.Present)
                };
            }

            // Exactly one side is the empty-slot default. The kernel treats a max speed of 0 as the STRONGEST clamp
            // (freeze) and only a negative value as "no clamp", so lerping toward the zeroed default froze the body at
            // the blend edges (lerp(0, 10, 0.1) = 1). "No clamp" lives at +infinity, so it cannot be reached by a
            // finite lerp; instead relax the present clamp by its own weight (max / weight) — authored max at full
            // weight, rising toward no-clamp as the clip fades to 0 so the effect vanishes at the edges.
            var present = aEmpty ? b : a;
            var presentWeight = aEmpty ? s : 1f - s;

            return new PhysicsVelocityClampData
            {
                MaxLinearSpeed = Relax(present.MaxLinearSpeed, presentWeight),
                MaxAngularSpeed = Relax(present.MaxAngularSpeed, presentWeight),
                Present = 1
            };
        }

        public PhysicsVelocityClampData Add(in PhysicsVelocityClampData a, in PhysicsVelocityClampData b)
        {
            return b.Present != 0 ? b : a;
        }

        // Relax a clamp by its own weight: authored max at weight 1, rising toward "no clamp" as weight -> 0. A
        // negative max is already "disabled" (leave it), and an authored freeze (max == 0) divides to 0 so it stays a
        // freeze at any weight — honouring "an authored max of exactly 0 legitimately means freeze".
        private static float Relax(float max, float weight)
        {
            return max <= 0f ? max : max / math.max(weight, 1e-4f);
        }
    }
}