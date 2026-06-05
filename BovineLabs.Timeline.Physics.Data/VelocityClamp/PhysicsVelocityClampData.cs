using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsVelocityClampData : IComponentData
    {
        public float MaxLinearSpeed;
        public float MaxAngularSpeed;
    }

    public struct PhysicsVelocityClampAnimated : IAnimatedComponent<PhysicsVelocityClampData>
    {
        public PhysicsVelocityClampData AuthoredData;
        public PhysicsVelocityClampData Value { get; set; }
    }

    public struct ActiveVelocityClamp : IComponentData, IEnableableComponent
    {
        public PhysicsVelocityClampData Config;
    }

    public struct PhysicsVelocityClampState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsVelocityClampMixer : IMixer<PhysicsVelocityClampData>
    {
        public PhysicsVelocityClampData Lerp(in PhysicsVelocityClampData a, in PhysicsVelocityClampData b, in float s)
        {
            return new PhysicsVelocityClampData
            {
                MaxLinearSpeed = math.lerp(a.MaxLinearSpeed, b.MaxLinearSpeed, s),
                MaxAngularSpeed = math.lerp(a.MaxAngularSpeed, b.MaxAngularSpeed, s)
            };
        }

        public PhysicsVelocityClampData Add(in PhysicsVelocityClampData a, in PhysicsVelocityClampData b)
        {
            return b;
        }
    }
}