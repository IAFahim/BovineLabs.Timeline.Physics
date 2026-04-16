using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsDragData
    {
        public float Linear;
        public float Angular;
    }

    public struct PhysicsDragAnimated : IAnimatedComponent<PhysicsDragData>
    {
        public PhysicsDragData AuthoredData;
        [CreateProperty] public PhysicsDragData Value { get; set; }
    }

    public struct ActiveDrag : IComponentData, IEnableableComponent
    {
        public PhysicsDragData Config;
    }

    public readonly struct PhysicsDragMixer : IMixer<PhysicsDragData>
    {
        public PhysicsDragData Lerp(in PhysicsDragData a, in PhysicsDragData b, in float s) => new()
        {
            Linear = math.lerp(a.Linear, b.Linear, s),
            Angular = math.lerp(a.Angular, b.Angular, s)
        };

        public PhysicsDragData Add(in PhysicsDragData a, in PhysicsDragData b) => new()
        {
            Linear = a.Linear + b.Linear,
            Angular = a.Angular + b.Angular
        };
    }
}
