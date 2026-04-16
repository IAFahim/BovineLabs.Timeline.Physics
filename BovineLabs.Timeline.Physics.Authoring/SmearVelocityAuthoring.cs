// SmearVelocityAuthoring.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Smear
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Smear/Smear Velocity Authoring")]
    public class SmearVelocityAuthoring : MonoBehaviour
    {
        // No editable fields needed — velocity is driven entirely at runtime
        // by UpdateSmearVelocitySystem via PhysicsVelocity.

        class Baker : Baker<SmearVelocityAuthoring>
        {
            public override void Bake(SmearVelocityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Initialise to zero; UpdateSmearVelocitySystem fills it each frame
                AddComponent(entity, new SmearVelocity { Value = float4.zero });
            }
        }
    }
}