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