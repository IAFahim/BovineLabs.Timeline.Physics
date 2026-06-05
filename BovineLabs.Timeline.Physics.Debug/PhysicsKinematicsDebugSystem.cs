using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Physics.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR || BL_DEBUG
namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class PhysicsKinematicsDebugSystemConfig
    {
        [ConfigVar("physicskinematics.draw-enabled", false,
            "Enable the Kinematics debug drawer in the editor.")]
        public static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        private struct Tags
        {
            public struct Enabled
            {
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsKinematicsDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<PhysicsMass> _massLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            _massLookup = state.GetComponentLookup<PhysicsMass>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsKinematicsDebugSystem>(
                    ref state, PhysicsKinematicsDebugSystemConfig.Enabled.Data, out var drawer))
                return;

            var gravity = SystemAPI.HasSingleton<PhysicsStep>()
                ? SystemAPI.GetSingleton<PhysicsStep>().Gravity
                : new float3(0, -9.81f, 0);

            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _massLookup.Update(ref state);
            _targetsLookup.Update(ref state);

            state.Dependency = new DrawActiveForceJob
            {
                Drawer = drawer,
                Gravity = gravity,
                LocalToWorldLookup = _localToWorldLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                VelocityLookup = _velocityLookup,
                MassLookup = _massLookup,
                TargetsLookup = _targetsLookup
            }.Schedule(state.Dependency);

            state.Dependency = new DrawActiveVelocityJob
            {
                Drawer = drawer,
                Gravity = gravity,
                LocalToWorldLookup = _localToWorldLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                VelocityLookup = _velocityLookup,
                TargetsLookup = _targetsLookup
            }.Schedule(state.Dependency);
        }


        [BurstCompile]
        private partial struct DrawActiveForceJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Gravity;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<PhysicsMass> MassLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;

            private static readonly Color ColorForce = TimelineDebugColors.LinearForce;

            private void Execute(Entity entity, in ActiveForce active, in PhysicsForceState state)
            {
                var pos = PhysicsMath.ResolvePosition(entity, in LocalTransformLookup, in LocalToWorldLookup,
                    in ParentLookup);
                if (math.lengthsq(pos) < 1e-6f) return;

                PhysicsMath.ResolveSpaceVector(active.Config.Space, active.Config.Linear, entity,
                    in TargetsLookup, in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup,
                    out var forceVec);

                var massInv = MassLookup.TryGetComponent(entity, out var m) ? m.InverseMass : 1f;
                var baseVel = VelocityLookup.TryGetComponent(entity, out var v) ? v.Linear : float3.zero;

                Drawer.Arrow(pos, forceVec * massInv, ColorForce);

                var vel = baseVel;

                if (active.Config.Mode == PhysicsForceMode.Impulse && !state.Fired)
                    vel += forceVec * massInv;

                const float dt = 0.05f;
                var steps = (int)(2f / dt);
                for (var i = 0; i < steps; i++)
                {
                    var accel = Gravity;
                    if (active.Config.Mode == PhysicsForceMode.Continuous)
                        accel += forceVec * massInv;

                    vel += accel * dt;
                    var nextPos = pos + vel * dt;
                    Drawer.Line(pos, nextPos, new Color(ColorForce.r, ColorForce.g, ColorForce.b, 0.5f));
                    pos = nextPos;
                }

                Drawer.Point(pos, 0.2f, Color.red);
            }
        }

        [BurstCompile]
        private partial struct DrawActiveVelocityJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Gravity;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;

            private static readonly Color ColorVel = TimelineDebugColors.LinearVelocity;

            private void Execute(Entity entity, in ActiveVelocity active, in PhysicsVelocityState state)
            {
                var pos = PhysicsMath.ResolvePosition(entity, in LocalTransformLookup, in LocalToWorldLookup,
                    in ParentLookup);
                if (math.lengthsq(pos) < 1e-6f) return;

                PhysicsMath.ResolveSpaceVector(active.Config.Space, active.Config.Linear, entity,
                    in TargetsLookup, in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup,
                    out var targetVel);

                var baseVel = VelocityLookup.TryGetComponent(entity, out var v) ? v.Linear : float3.zero;
                Drawer.Arrow(pos, targetVel, ColorVel);

                var vel = baseVel;

                var isInstant = active.Config.Mode == PhysicsVelocityMode.SetInstant ||
                                active.Config.Mode == PhysicsVelocityMode.AddInstant;
                var isSet = active.Config.Mode == PhysicsVelocityMode.SetContinuous ||
                            active.Config.Mode == PhysicsVelocityMode.SetInstant;

                if (isInstant && !state.Fired)
                    vel = isSet ? targetVel : vel + targetVel;

                const float dt = 0.05f;
                var steps = (int)(2f / dt);

                for (var i = 0; i < steps; i++)
                {
                    if (!isInstant)
                    {
                        if (isSet) vel = targetVel;
                        else vel += targetVel * dt;
                    }

                    if (isInstant || !isSet)
                        vel += Gravity * dt;

                    var nextPos = pos + vel * dt;
                    Drawer.Line(pos, nextPos, new Color(ColorVel.r, ColorVel.g, ColorVel.b, 0.5f));
                    pos = nextPos;
                }

                Drawer.Point(pos, 0.2f, Color.red);
            }
        }
    }
}
#endif