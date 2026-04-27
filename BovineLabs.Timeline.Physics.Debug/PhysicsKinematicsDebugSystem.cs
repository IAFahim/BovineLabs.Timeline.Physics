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
    public partial struct PhysicsKinematicsDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> _localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomsLookup = state.GetComponentLookup<TargetsCustom>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<DrawSystem.Singleton>(out var drawSystem)) return;

            var drawer = drawSystem.CreateDrawer();
            var gravity = SystemAPI.HasSingleton<PhysicsStep>() ? SystemAPI.GetSingleton<PhysicsStep>().Gravity : new float3(0, -9.81f, 0);

            _localTransformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _targetsCustomsLookup.Update(ref state);

            state.Dependency = new DrawForceJob
            {
                Drawer = drawer,
                Gravity = gravity,
                TransformLookup = _localTransformLookup,
                VelocityLookup = _velocityLookup,
                MassLookup = _massLookup,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomsLookup
            }.Schedule(state.Dependency);

            state.Dependency = new DrawVelocityJob
            {
                Drawer = drawer,
                Gravity = gravity,
                TransformLookup = _localTransformLookup,
                VelocityLookup = _velocityLookup,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawForceJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Gravity;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;

            private void Execute(in TrackBinding binding, in PhysicsForceAnimated animated, in PhysicsForceState state)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!PhysicsMath.TryResolveSpaceVector(animated.AuthoredData.Space, animated.AuthoredData.Linear, entity, in TargetsLookup,
                        in TargetsCustomLookup, in TransformLookup, out var forceVec)) return;

                var massInv = MassLookup.TryGetComponent(entity, out var m) ? m.InverseMass : 1f;
                var baseVel = VelocityLookup.TryGetComponent(entity, out var v) ? v.Linear : float3.zero;

                Drawer.Arrow(transform.Position, forceVec * massInv, Color.magenta);

                var pos = transform.Position;
                var vel = baseVel;
                
                if (animated.AuthoredData.Mode == PhysicsForceMode.Impulse && !state.Fired) 
                    vel += forceVec * massInv;

                const float dt = 0.05f;
                var steps = (int)(2f / dt);
                for (var i = 0; i < steps; i++)
                {
                    var accel = Gravity;
                    if (animated.AuthoredData.Mode == PhysicsForceMode.Continuous) 
                        accel += forceVec * massInv;

                    vel += accel * dt;
                    var nextPos = pos + vel * dt;
                    Drawer.Line(pos, nextPos, new Color(1f, 0f, 1f, 0.5f));
                    pos = nextPos;
                }
                Drawer.Point(pos, 0.2f, Color.red);
            }
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawVelocityJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Gravity;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;

            private void Execute(in TrackBinding binding, in PhysicsVelocityAnimated animated, in PhysicsVelocityState state)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                if (!PhysicsMath.TryResolveSpaceVector(animated.AuthoredData.Space, animated.AuthoredData.Linear, entity, in TargetsLookup,
                        in TargetsCustomLookup, in TransformLookup, out var targetVel)) return;

                var baseVel = VelocityLookup.TryGetComponent(entity, out var v) ? v.Linear : float3.zero;
                Drawer.Arrow(transform.Position, targetVel, Color.cyan);

                var pos = transform.Position;
                var vel = baseVel;

                var isInstant = animated.AuthoredData.Mode == PhysicsVelocityMode.SetInstant || animated.AuthoredData.Mode == PhysicsVelocityMode.AddInstant;
                var isSet = animated.AuthoredData.Mode == PhysicsVelocityMode.SetContinuous || animated.AuthoredData.Mode == PhysicsVelocityMode.SetInstant;

                if (isInstant && !state.Fired)
                    vel = isSet ? targetVel : vel + targetVel;

                const float dt = 0.05f;
                var steps = (int)(2f / dt);
                
                for (var i = 0; i < steps; i++)
                {
                    if (!isInstant)
                    {
                        if (isSet) vel = targetVel; // Overrides gravity
                        else vel += targetVel * dt; // Continuous add
                    }
                    
                    if (isInstant || !isSet) 
                        vel += Gravity * dt; // Gravity applies if not SetContinuous

                    var nextPos = pos + vel * dt;
                    Drawer.Line(pos, nextPos, new Color(0f, 1f, 1f, 0.5f));
                    pos = nextPos;
                }
                Drawer.Point(pos, 0.2f, Color.red);
            }
        }
    }
}
#endif
