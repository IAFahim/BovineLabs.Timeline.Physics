using BovineLabs.Core;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
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
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer();

            state.Dependency = new DrawPIDJob
            {
                Drawer = drawer,
                TransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true),
                TargetsLookup = SystemAPI.GetComponentLookup<Targets>(true)
            }.Schedule(state.Dependency); 
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawPIDJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;

            private void Execute(in TrackBinding binding, in PhysicsPIDAnimated animated)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                var data = animated.AuthoredData;

                // Predict Destination
                var selfGoal = transform.Position + math.rotate(transform.Rotation, data.LocalTargetOffset);
                var finalGoal = selfGoal;

                if (data.ChaseTargetBlend > 0.001f && TargetsLookup.TryGetComponent(entity, out var targets))
                {
                    if (TransformLookup.TryGetComponent(targets.Target, out var enemyTransform))
                    {
                        var enemyGoal = enemyTransform.Position + math.rotate(enemyTransform.Rotation, data.LocalTargetOffset);
                        finalGoal = math.lerp(selfGoal, enemyGoal, data.ChaseTargetBlend);
                    }
                }

                // Draw Path & Target
                Drawer.Line(transform.Position, finalGoal, Color.yellow);
                Drawer.Point(finalGoal, 0.2f, Color.red);
                Drawer.Text32(finalGoal + new float3(0, 0.4f, 0), "PID Goal", Color.yellow, 12f);

                // Draw Current Trajectory (Velocity Vector)
                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                {
                    Drawer.Arrow(transform.Position, velocity.Linear, Color.cyan);
                }
            }
        }
    }
}