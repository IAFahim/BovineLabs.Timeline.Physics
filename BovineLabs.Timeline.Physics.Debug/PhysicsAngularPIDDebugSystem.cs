#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core.Debug;
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
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class PhysicsAngularPIDDebugSystemConfig
    {
        [ConfigVar("physicspid.angular.draw-enabled", false,
            "Enable the Angular PID debug drawer in the editor.")]
        public static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        private struct Tags
        {
            public struct Enabled { }
        }
    }


    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsAngularPIDDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<Targets> _targetsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsAngularPIDDebugSystem>(
                    ref state, PhysicsAngularPIDDebugSystemConfig.Enabled.Data, out var drawer))
                return;

            _localToWorldLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _targetsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                TransformLookup = _localToWorldLookup,
                VelocityLookup = _velocityLookup,
                TargetsLookup = _targetsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;

            private static readonly Color ColorForward = Color.blue;
            private static readonly Color ColorUp      = Color.green;
            private static readonly Color ColorAngVel = TimelineDebugColors.AngularForce;
            private static readonly Color ColorPred   = TimelineDebugColors.PidPredicted;

            private void Execute(in TrackBinding binding, in PhysicsAngularPIDAnimated animated, in LocalTime localTime)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                PhysicsMath.ResolveAngularPidTarget(transform, animated.AuthoredData, entity, in TargetsLookup,
                    in TransformLookup, out var finalRot);

                var forward = math.mul(finalRot, math.forward());
                var up = math.mul(finalRot, math.up());
                Drawer.Arrow(transform.Position, forward, ColorForward);
                Drawer.Arrow(transform.Position, up, ColorUp);

                PhysicsMath.DrawAngularPidPrediction(ref Drawer, transform.Position, new quaternion(transform.Value),
                    finalRot, animated.AuthoredData.Tuning, (float)localTime.Value);

                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                    Drawer.Arrow(transform.Position, velocity.Angular, ColorAngVel);
            }
        }
    }
}
#endif
