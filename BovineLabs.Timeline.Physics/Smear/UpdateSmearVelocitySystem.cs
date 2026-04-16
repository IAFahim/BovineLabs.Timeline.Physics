using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Rendering;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Smear
{
    [BurstCompile]                          // add this
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct UpdateSmearVelocitySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Query all entities that have both PhysicsVelocity and our Shader Component
            foreach (var (physicsVel, smearVel) in SystemAPI.Query<RefRO<PhysicsVelocity>, RefRW<SmearVelocity>>())
            {
                // Physics velocity is a float3, we pack it into a float4 for the shader
                smearVel.ValueRW.Value = new float4(physicsVel.ValueRO.Linear, 0f);
            }
        }
    }
}
