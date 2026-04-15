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
    public partial struct PhysicsAngularPIDDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> velocityLookup;
        private UnsafeComponentLookup<Targets> targetsLookup;
        private ComponentLookup<TargetsCustom> targetsCustomsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            targetsCustomsLookup = state.GetComponentLookup<TargetsCustom>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var drawer = SystemAPI.GetSingleton<DrawSystem.Singleton>().CreateDrawer();

            localTransformLookup.Update(ref state);
            velocityLookup.Update(ref state);
            targetsLookup.Update(ref state);
            targetsCustomsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                TransformLookup = localTransformLookup,
                VelocityLookup = velocityLookup,
                TargetsLookup = targetsLookup,
                TargetsCustomLookup = targetsCustomsLookup
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
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;

            private void Execute(in TrackBinding binding, in PhysicsAngularPIDAnimated animated)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!PhysicsMath.TryResolveAngularPidTarget(transform, animated.AuthoredData, entity, TargetsLookup, TargetsCustomLookup, TransformLookup, out var finalRot)) return;
                
                var forward = math.mul(finalRot, math.forward());
                var up = math.mul(finalRot, math.up());
                Drawer.Arrow(transform.Position, forward, Color.blue);
                Drawer.Arrow(transform.Position, up, Color.green);

                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                    Drawer.Arrow(transform.Position, velocity.Angular, Color.magenta);
            }
        }
    }
}
#endif