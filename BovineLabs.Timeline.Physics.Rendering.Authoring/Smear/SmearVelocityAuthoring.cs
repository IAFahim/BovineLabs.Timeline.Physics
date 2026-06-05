using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Physics.Smear;
using Unity.Entities;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("BovineLabs/Smear/Smear Velocity Authoring")]
    public class SmearVelocityAuthoring : MonoBehaviour
    {
        private class Baker : Baker<SmearVelocityAuthoring>
        {
            public override void Bake(SmearVelocityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var commands = new BakerCommands(this, entity);
                var builder = new SmearVelocityBuilder();
                builder.ApplyTo(ref commands);
            }
        }
    }
}