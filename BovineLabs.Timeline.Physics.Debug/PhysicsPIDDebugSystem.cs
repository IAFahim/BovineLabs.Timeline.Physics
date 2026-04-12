#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
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
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> velocityLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            this.velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            this.targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer();

            this.localTransformLookup.Update(ref state);
            this.velocityLookup.Update(ref state);
            this.targetsLookup.Update(ref state);

            state.Dependency = new DrawPIDJob
            {
                Drawer = drawer,
                TransformLookup = this.localTransformLookup,
                VelocityLookup = this.velocityLookup,
                TargetsLookup = this.targetsLookup
            }.Schedule(state.Dependency); 
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawPIDJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;

            private void Execute(in TrackBinding binding, in PhysicsPIDAnimated animated)
            {
                var entity = binding.Value;
                if (!this.TransformLookup.TryGetComponent(entity, out var transform)) return;

                var data = animated.AuthoredData;

                var selfGoal = transform.Position + math.rotate(transform.Rotation, data.LocalTargetOffset);
                var finalGoal = selfGoal;

                if (data.ChaseTargetBlend > 0.001f && this.TargetsLookup.TryGetComponent(entity, out var targets))
                {
                    if (this.TransformLookup.TryGetComponent(targets.Target, out var enemyTransform))
                    {
                        var enemyGoal = enemyTransform.Position + math.rotate(enemyTransform.Rotation, data.LocalTargetOffset);
                        finalGoal = math.lerp(selfGoal, enemyGoal, data.ChaseTargetBlend);
                    }
                }

                this.Drawer.Line(transform.Position, finalGoal, Color.yellow);
                this.Drawer.Point(finalGoal, 0.2f, Color.red);
                this.Drawer.Text32(finalGoal + new float3(0, 0.4f, 0), "PID Goal", Color.yellow, 12f);

                if (this.VelocityLookup.TryGetComponent(entity, out var velocity))
                {
                    this.Drawer.Arrow(transform.Position, velocity.Linear, Color.cyan);
                }
            }
        }
    }
}
#endif