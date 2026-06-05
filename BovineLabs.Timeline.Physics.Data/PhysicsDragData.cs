using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics.Data
{
    public struct PhysicsDragData
    {
        public float Linear;
        public float Angular;
        public StatStrengthConfig Strength;
    }

    public struct PhysicsDragAnimated : IAnimatedComponent<PhysicsDragData>, IPreparable
    {
        public PhysicsDragData AuthoredData;
        [CreateProperty] public PhysicsDragData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveDrag : IActive<PhysicsDragData>
    {
        public PhysicsDragData Config { get; set; }
    }

    public readonly struct PhysicsDragMixer : IMixer<PhysicsDragData>
    {
        public PhysicsDragData Lerp(in PhysicsDragData a, in PhysicsDragData b, in float s)
        {
            return new PhysicsDragData
            {
                Linear = math.lerp(a.Linear, b.Linear, s),
                Angular = math.lerp(a.Angular, b.Angular, s),
                Strength = s < 0.5f ? a.Strength : b.Strength
            };
        }

        public PhysicsDragData Add(in PhysicsDragData a, in PhysicsDragData b)
        {
            return new PhysicsDragData
            {
                Linear = a.Linear + b.Linear,
                Angular = a.Angular + b.Angular,
                Strength = a.Strength
            };
        }
    }
}