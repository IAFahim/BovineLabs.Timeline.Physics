#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
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
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class PhysicsAngularPIDDebugSystemConfig
    {
        private const string DrawForced = "physicsangularpiddebugsystem.force-draw";
        private const string DrawGlobalDescEnabled = "Enable the drawer in the editor.";

        [ConfigVar(DrawForced, false, DrawGlobalDescEnabled)]
        internal static readonly SharedStatic<bool> Enabled =
            SharedStatic<bool>.GetOrCreate<PhysicsAngularPIDDebugSystemForced>();

        private struct PhysicsAngularPIDDebugSystemForced
        {
        }
    }


    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsAngularPIDDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalTransform> _localTransformLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private ComponentLookup<Targets> _targetsLookup;
        private ComponentLookup<TargetsCustom> _targetsCustomsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _localTransformLookup = state.GetUnsafeComponentLookup<LocalTransform>(true);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            _targetsLookup = state.GetComponentLookup<Targets>(true);
            _targetsCustomsLookup = state.GetComponentLookup<TargetsCustom>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<DrawSystem.Singleton>()) return;
            ref var drawSystem = ref SystemAPI.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

            Drawer drawer;
            if (!PhysicsAngularPIDDebugSystemConfig.Enabled.Data)
            {
                drawer = drawSystem.CreateDrawer<PhysicsAngularPIDDebugSystem>();
                if (!drawer.IsEnabled) return;
            }
            else
            {
                drawer = drawSystem.CreateDrawer();
            }

            _localTransformLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _targetsCustomsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                TransformLookup = _localTransformLookup,
                VelocityLookup = _velocityLookup,
                TargetsLookup = _targetsLookup,
                TargetsCustomLookup = _targetsCustomsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            [ReadOnly] public UnsafeComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public ComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public ComponentLookup<TargetsCustom> TargetsCustomLookup;

            private void Execute(in TrackBinding binding, in PhysicsAngularPIDAnimated animated, in LocalTime localTime)
            {
                var entity = binding.Value;
                if (!TransformLookup.TryGetComponent(entity, out var transform)) return;

                PhysicsMath.ResolveAngularPidTarget(transform, animated.AuthoredData, entity, in TargetsLookup,
                    in TargetsCustomLookup, in TransformLookup, out var finalRot);

                var forward = math.mul(finalRot, math.forward());
                var up = math.mul(finalRot, math.up());
                Drawer.Arrow(transform.Position, forward, Color.blue);
                Drawer.Arrow(transform.Position, up, Color.green);

                PhysicsMath.DrawAngularPidPrediction(ref Drawer, transform.Position, transform.Rotation, finalRot,
                    animated.AuthoredData.Tuning, (float)localTime.Value);

                if (VelocityLookup.TryGetComponent(entity, out var velocity))
                    Drawer.Arrow(transform.Position, velocity.Angular, Color.magenta);
            }
        }
    }
}
#endif