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
using BovineLabs.Timeline.Physics.Infrastructure;
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
    public static class PhysicsLinearPIDDebugSystemConfig
    {
        [ConfigVar("physicspid.linear.draw-enabled", false,
            "Enable the Linear PID debug drawer in the editor.")]
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
    public partial struct PhysicsLinearPIDDebugSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<PhysicsVelocity> _velocityLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _velocityLookup = state.GetUnsafeComponentLookup<PhysicsVelocity>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsLinearPIDDebugSystem>(
                    ref state, PhysicsLinearPIDDebugSystemConfig.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _velocityLookup.Update(ref state);
            _targetsLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LocalToWorldLookup = _localToWorldLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                VelocityLookup = _velocityLookup,
                TargetsLookup = _targetsLookup
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsVelocity> VelocityLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;

            private static readonly Color ColorLine = TimelineDebugColors.PidTarget;
            private static readonly Color ColorGoal = TimelineDebugColors.PidGoal;
            private static readonly Color ColorVel = TimelineDebugColors.LinearVelocity;

            private void Execute(in TrackBinding binding, in PhysicsLinearPIDAnimated animated, in LocalTime localTime)
            {
                var entity = binding.Value;
                var selfPos = PhysicsMath.ResolvePosition(entity, in LocalTransformLookup, in LocalToWorldLookup,
                    in ParentLookup);
                if (math.lengthsq(selfPos) < 1e-6f) return;

                PhysicsMath.ResolveLinearPidTarget(animated.AuthoredData, entity, in TargetsLookup,
                    in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var finalPos);

                var tier = TimelineDebugTier.Resolve(selfPos, Viewer, HasViewer);

                // Far: what the system does — the body is being pulled to the goal.
                Drawer.Line(selfPos, finalPos, ColorLine);
                Drawer.Point(finalPos, 0.2f, ColorGoal);

                if (tier >= DebugTier.Mid)
                {
                    // Mid: the live interaction — current velocity + a label.
                    Drawer.Text32(finalPos + new float3(0, 0.4f, 0), "Linear PID Goal", ColorLine, 12f);
                    if (VelocityLookup.TryGetComponent(entity, out var velocity))
                        Drawer.Arrow(selfPos, velocity.Linear, ColorVel);
                }

                if (tier == DebugTier.Close)
                {
                    // Close: every number — predicted path + error distance + the PID gains.
                    PhysicsMath.DrawLinearPidPrediction(ref Drawer, selfPos, finalPos,
                        animated.AuthoredData.Tuning, (float)localTime.Value);

                    var tuning = animated.AuthoredData.Tuning;
                    var readout = new FixedString128Bytes();
                    readout.Append((FixedString32Bytes)"err ");
                    readout.Append(math.distance(selfPos, finalPos));
                    readout.Append((FixedString32Bytes)"m  P");
                    readout.Append(tuning.Proportional.x);
                    readout.Append((FixedString32Bytes)" D");
                    readout.Append(tuning.Derivative.x);
                    readout.Append((FixedString32Bytes)" I");
                    readout.Append(tuning.Integral.x);
                    Drawer.Text128(selfPos + new float3(0, 0.7f, 0), readout, TimelineDebugColors.Label, 11f);
                }
            }
        }
    }
}
#endif