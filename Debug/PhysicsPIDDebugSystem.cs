using BovineLabs.Core;
using BovineLabs.Quill;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsPIDDebugSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer<PhysicsPIDDebugSystem>();
            if (!drawer.IsEnabled) return;

            state.Dependency = new DrawPIDJob
            {
                Drawer = drawer,
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true)
            }.Schedule(state.Dependency); // Schedule single due to Quill drawer
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawPIDJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;

            private void Execute(in TrackBinding binding, in PhysicsPIDAnimated animated)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                // Predict the "Carrot" target position
                var targetPos = transform.Position + math.rotate(transform.Rotation, animated.AuthoredData.LocalTargetOffset);

                // Draw Line to Target
                Drawer.Line(transform.Position, targetPos, Color.green);
                Drawer.Point(targetPos, 0.1f, Color.red);
                Drawer.Text32(targetPos + new float3(0, 0.2f, 0), "PID Target", Color.green, 12f);

                // Draw Current Velocity Vector
                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                {
                    Drawer.Arrow(transform.Position, velocity.Linear * 0.5f, Color.cyan);
                }
            }
        }
    }
}