using BovineLabs.Core.EntityCommands;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Smear
{
    public struct SmearVelocityBuilder
    {
        public float4 InitialValue;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new SmearVelocity { Value = InitialValue });
        }
    }
}