using BovineLabs.Core.Authoring.EntityCommands;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Smear
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Smear/Smear Velocity Authoring")]
    public class SmearVelocityAuthoring : MonoBehaviour
    {
        private class Baker : Baker<SmearVelocityAuthoring>
        {
            public override void Bake(SmearVelocityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var commands = new BakerCommands(this, entity);
                var builder = new SmearVelocityBuilder()
                    .WithInitialValue(float4.zero);
                builder.ApplyTo(ref commands);
            }
        }
    }
}