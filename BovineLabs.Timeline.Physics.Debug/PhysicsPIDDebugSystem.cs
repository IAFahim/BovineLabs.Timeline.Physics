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
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsLinearPIDDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> velocityLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer();

            localTransformLookup.Update(ref state);
            velocityLookup.Update(ref state);
            targetsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                TransformLookup = localTransformLookup,
                VelocityLookup = velocityLookup,
                TargetsLookup = targetsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;

            private void Execute(in TrackBinding binding, in PhysicsLinearPIDAnimated animated)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!PhysicsMath.TryResolveLinearPidTarget(transform, animated.AuthoredData, entity, TargetsLookup, TransformLookup, out var finalPos)) return;

                Drawer.Line(transform.Position, finalPos, Color.yellow);
                Drawer.Point(finalPos, 0.2f, Color.red);
                Drawer.Text32(finalPos + new float3(0, 0.4f, 0), "Linear PID Goal", Color.yellow, 12f);

                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                    Drawer.Arrow(transform.Position, velocity.Linear, Color.cyan);
            }
        }
    }
}
#endif