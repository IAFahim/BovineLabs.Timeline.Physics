#if UNITY_EDITOR || BL_DEBUG
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

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class PhysicsForceDebugSystemConfig
    {
        [ConfigVar("physicsforce.draw-enabled", false, "Enable the Physics Force track debug drawer.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

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
    public partial struct PhysicsForceDebugSystem : ISystem
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
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsForceDebugSystem>(
                    ref state, PhysicsForceDebugSystemConfig.Enabled.Data, out var drawer,
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

            private const float ArrowLen = 2f;

            private void Execute(in TrackBinding binding, in PhysicsForceAnimated animated)
            {
                var body = binding.Value;
                if (body == Entity.Null || !LocalToWorldLookup.HasComponent(body)) return;

                var cfg = animated.AuthoredData;
                var selfPos = PhysicsMath.ResolvePosition(body, in LocalTransformLookup, in LocalToWorldLookup,
                    in ParentLookup);
                var tier = TimelineDebugTier.Resolve(selfPos, Viewer, HasViewer);

                Drawer.Point(selfPos, 0.12f, TimelineDebugColors.LinearForce);

                // Geometry per direction mode — drawn from the authored config (the same fields the apply system reads).
                switch (cfg.DirectionMode)
                {
                    case PhysicsForceDirectionMode.FixedVector:
                        DrawSpaceArrow(body, cfg.Space, cfg.Linear, selfPos);
                        break;

                    case PhysicsForceDirectionMode.TowardTarget:
                    case PhysicsForceDirectionMode.AwayFromTarget:
                        DrawTargetArrow(body, in cfg, selfPos);
                        break;

                    case PhysicsForceDirectionMode.RandomSphere:
                        Drawer.Sphere(selfPos, ArrowLen, 12, TimelineDebugColors.LinearForce);
                        break;

                    case PhysicsForceDirectionMode.RandomCone:
                        DrawCone(body, in cfg, selfPos);
                        break;

                    case PhysicsForceDirectionMode.AlongVelocity:
                    case PhysicsForceDirectionMode.AgainstVelocity:
                        DrawVelocityArrow(body, in cfg, selfPos);
                        break;
                }

                if (math.lengthsq(cfg.Angular) > 1e-5f)
                {
                    // Angular shown in the body's frame regardless of linear space.
                    PhysicsMath.ResolveSpaceVector(Target.Self, math.normalizesafe(cfg.Angular), body,
                        in TargetsLookup, in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup,
                        out var ang);
                    Drawer.Arrow(selfPos, ang * 1.25f, TimelineDebugColors.AngularForce);
                }

                if (tier >= DebugTier.Mid)
                    DrawLabel(in cfg, selfPos, tier);
            }

            private void DrawSpaceArrow(Entity body, Target space, float3 vector, float3 selfPos)
            {
                if (math.lengthsq(vector) < 1e-6f) return;
                PhysicsMath.ResolveSpaceVector(space, math.normalize(vector), body, in TargetsLookup,
                    in LocalTransformLookup, in LocalToWorldLookup, in ParentLookup, out var dir);
                Drawer.Arrow(selfPos, dir * ArrowLen, TimelineDebugColors.LinearForce);
            }

            private void DrawTargetArrow(Entity body, in PhysicsForceData cfg, float3 selfPos)
            {
                var targetEntity = body;
                if (cfg.DirectionTarget != Target.None &&
                    TargetsLookup.TryGetComponent(body, out var targets))
                {
                    // ponytail: resolves only the base Target, not the link override (DirectionTargetLinkKey).
                    // Add link resolution if a gizmo needs to follow a remapped link target.
                    var t = targets.Get(cfg.DirectionTarget, body);
                    if (t != Entity.Null) targetEntity = t;
                }

                var targetPos = PhysicsMath.ResolvePosition(targetEntity, in LocalTransformLookup,
                    in LocalToWorldLookup, in ParentLookup);
                var diff = targetPos - selfPos;
                if (math.lengthsq(diff) < 1e-5f) return;

                var dir = math.normalize(diff);
                if (cfg.DirectionMode == PhysicsForceDirectionMode.AwayFromTarget) dir = -dir;

                Drawer.Line(selfPos, targetPos, TimelineDebugColors.TargetLink);
                Drawer.Point(targetPos, 0.15f, TimelineDebugColors.TargetLink);
                Drawer.Arrow(selfPos, dir * ArrowLen, TimelineDebugColors.LinearForce);
            }

            private void DrawVelocityArrow(Entity body, in PhysicsForceData cfg, float3 selfPos)
            {
                if (!VelocityLookup.TryGetComponent(body, out var velocity)) return;
                var linear = velocity.Linear;
                if (math.lengthsq(linear) < 1e-8f) return;

                Drawer.Arrow(selfPos, linear, TimelineDebugColors.LinearVelocity);

                var dir = math.normalize(linear);
                if (cfg.DirectionMode == PhysicsForceDirectionMode.AgainstVelocity) dir = -dir;
                Drawer.Arrow(selfPos, dir * ArrowLen, TimelineDebugColors.LinearForce);
            }

            private void DrawCone(Entity body, in PhysicsForceData cfg, float3 selfPos)
            {
                // Four edges + centre of the cone, in the Space frame (radians already baked into the config).
                DrawConeRay(body, cfg.Space, cfg.ConeAzimuthCenter, cfg.ConeElevationCenter, selfPos,
                    TimelineDebugColors.LinearForce);

                var az = cfg.ConeAzimuthHalfRange;
                var el = cfg.ConeElevationHalfRange;
                var edge = TimelineDebugColors.LinearForce;
                edge.a *= 0.5f;
                DrawConeRay(body, cfg.Space, cfg.ConeAzimuthCenter + az, cfg.ConeElevationCenter + el, selfPos, edge);
                DrawConeRay(body, cfg.Space, cfg.ConeAzimuthCenter - az, cfg.ConeElevationCenter + el, selfPos, edge);
                DrawConeRay(body, cfg.Space, cfg.ConeAzimuthCenter + az, cfg.ConeElevationCenter - el, selfPos, edge);
                DrawConeRay(body, cfg.Space, cfg.ConeAzimuthCenter - az, cfg.ConeElevationCenter - el, selfPos, edge);
            }

            private void DrawConeRay(Entity body, Target space, float az, float el, float3 selfPos, UnityEngine.Color c)
            {
                var cosEl = math.cos(el);
                var local = new float3(cosEl * math.sin(az), math.sin(el), cosEl * math.cos(az));
                PhysicsMath.ResolveSpaceVector(space, local, body, in TargetsLookup, in LocalTransformLookup,
                    in LocalToWorldLookup, in ParentLookup, out var dir);
                Drawer.Arrow(selfPos, dir * ArrowLen, c);
            }

            private void DrawLabel(in PhysicsForceData cfg, float3 selfPos, DebugTier tier)
            {
                var label = new FixedString128Bytes();
                label.Append(DirModeName(cfg.DirectionMode));
                label.Append(cfg.Mode == PhysicsForceMode.Impulse
                    ? (FixedString32Bytes)" [IMP] "
                    : (FixedString32Bytes)" [CONT] ");
                label.Append(cfg.DirectionMode == PhysicsForceDirectionMode.FixedVector
                    ? math.length(cfg.Linear)
                    : cfg.Magnitude);

                if (tier == DebugTier.Close)
                {
                    label.Append((FixedString32Bytes)"\nspace ");
                    label.Append(SpaceName(cfg.Space));
                    if (cfg.LatchDirection) label.Append((FixedString32Bytes)" latched");
                }

                Drawer.Text128(selfPos + new float3(0f, 0.5f, 0f), label, TimelineDebugColors.Label, 11f);
            }

            private static FixedString32Bytes DirModeName(PhysicsForceDirectionMode mode)
            {
                switch (mode)
                {
                    case PhysicsForceDirectionMode.FixedVector: return "Fixed";
                    case PhysicsForceDirectionMode.TowardTarget: return "Toward";
                    case PhysicsForceDirectionMode.AwayFromTarget: return "Away";
                    case PhysicsForceDirectionMode.RandomSphere: return "RandSphere";
                    case PhysicsForceDirectionMode.RandomCone: return "RandCone";
                    case PhysicsForceDirectionMode.AlongVelocity: return "AlongVel";
                    case PhysicsForceDirectionMode.AgainstVelocity: return "AgainstVel";
                    default: return "?";
                }
            }

            private static FixedString32Bytes SpaceName(Target space)
            {
                switch (space)
                {
                    case Target.Self: return "Self";
                    case Target.Owner: return "Owner";
                    case Target.Source: return "Source";
                    case Target.Target: return "Target";
                    case Target.None: return "World";
                    default: return "?";
                }
            }
        }
    }
}
#endif
