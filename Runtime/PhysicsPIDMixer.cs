using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public readonly struct PhysicsPIDMixer : IMixer<PhysicsPIDData>
    {
        public PhysicsPIDData Lerp(in PhysicsPIDData a, in PhysicsPIDData b, in float s)
        {
            return new PhysicsPIDData
            {
                Proportional = math.lerp(a.Proportional, b.Proportional, s),
                Integral = math.lerp(a.Integral, b.Integral, s),
                Derivative = math.lerp(a.Derivative, b.Derivative, s),
                LocalTargetOffset = math.lerp(a.LocalTargetOffset, b.LocalTargetOffset, s),
                MaxForce = math.lerp(a.MaxForce, b.MaxForce, s)
            };
        }

        public PhysicsPIDData Add(in PhysicsPIDData a, in PhysicsPIDData b)
        {
            return new PhysicsPIDData
            {
                Proportional = a.Proportional + b.Proportional,
                Integral = a.Integral + b.Integral,
                Derivative = a.Derivative + b.Derivative,
                LocalTargetOffset = a.LocalTargetOffset + b.LocalTargetOffset,
                MaxForce = a.MaxForce + b.MaxForce
            };
        }
    }
}