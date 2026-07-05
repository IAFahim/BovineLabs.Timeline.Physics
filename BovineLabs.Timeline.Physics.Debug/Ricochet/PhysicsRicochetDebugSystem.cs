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
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class RicochetDebugSystem
    {
        [ConfigVar("ricochetgizmo.draw-enabled", false, "Enable the ricochet gizmo drawer.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("ricochetgizmo.ray-color-1", 0.0f, 0.8f, 1.0f, 0.7f, "Color for alternating ray segments (Cyan)")]
        public static readonly SharedStatic<Color> RayColor1 = SharedStatic<Color>.GetOrCreate<Tags.RayColor1>();

        [ConfigVar("ricochetgizmo.ray-color-2", 0.6f, 0.2f, 1.0f, 0.7f, "Color for alternating ray segments (Purple)")]
        public static readonly SharedStatic<Color> RayColor2 = SharedStatic<Color>.GetOrCreate<Tags.RayColor2>();

        [ConfigVar("ricochetgizmo.terminal-color", 1.0f, 0.4f, 0.2f, 0.9f, "Color for terminal hit (Coral)")]
        public static readonly SharedStatic<Color>
            TerminalColor = SharedStatic<Color>.GetOrCreate<Tags.TerminalColor>();

        [ConfigVar("ricochetgizmo.origin-color", 1.0f, 1.0f, 1.0f, 0.8f, "Color for ray origin (White)")]
        public static readonly SharedStatic<Color> OriginColor = SharedStatic<Color>.GetOrCreate<Tags.OriginColor>();

        [ConfigVar("ricochetgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        [ConfigVar("ricochetgizmo.segments", 12, "Sphere arc segment count.")]
        public static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<Tags.Segments>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct RayColor1
            {
            }

            public struct RayColor2
            {
            }

            public struct TerminalColor
            {
            }

            public struct OriginColor
            {
            }

            public struct TextColor
            {
            }

            public struct Segments
            {
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    [BurstCompile]
    public partial struct PhysicsRicochetGizmoSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private ComponentLookup<LocalTransform> _localTransformLookup;
        private ComponentLookup<Parent> _parentLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<PhysicsCollider> _colliderLookup;
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();

            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _localTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            _parentLookup = state.GetComponentLookup<Parent>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _colliderLookup = state.GetUnsafeComponentLookup<PhysicsCollider>(true);

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsRicochetAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            _localTransformLookup.Update(ref state);
            _parentLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _colliderLookup.Update(ref state);

            if (!TimelineDebugUtility.TryGetDrawer<PhysicsRicochetGizmoSystem>(
                    ref state, RicochetDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            state.Dependency = new DrawRicochetJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                Segments = math.clamp(RicochetDebugSystem.Segments.Data, 4, 32),
                RayColor1 = RicochetDebugSystem.RayColor1.Data,
                RayColor2 = RicochetDebugSystem.RayColor2.Data,
                TerminalColor = RicochetDebugSystem.TerminalColor.Data,
                OriginColor = RicochetDebugSystem.OriginColor.Data,
                TextColor = RicochetDebugSystem.TextColor.Data,
                TransformLookup = _localToWorldLookup,
                LocalTransformLookup = _localTransformLookup,
                ParentLookup = _parentLookup,
                TargetsLookup = _targetsLookup,
                ColliderLookup = _colliderLookup,
                CollisionWorld = collisionWorld
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawRicochetJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public int Segments;
            public Color RayColor1;
            public Color RayColor2;
            public Color TerminalColor;
            public Color OriginColor;
            public Color TextColor;

            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;

            private float3 GetAntiJitterPosition(Entity e, float3 fallback)
            {
                if (LocalTransformLookup.HasComponent(e) && !ParentLookup.HasComponent(e))
                    return LocalTransformLookup[e].Position;
                return fallback;
            }

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<PhysicsCollider> ColliderLookup;

            [ReadOnly] public CollisionWorld CollisionWorld;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsRicochetAnimated animated)
            {
                var d = animated.AuthoredData;
                var targets = TargetsLookup.TryGetComponent(entity, out var t) ? t : default;

                var origin = float3.zero;
                var direction = math.forward();

                var originEntity = ResolveTarget(entity, d.RayOrigin.ReadRootFrom, targets);
                if (originEntity != Entity.Null && TransformLookup.HasComponent(originEntity))
                    origin = GetAntiJitterPosition(originEntity, TransformLookup[originEntity].Position);

                var dirEntity = ResolveTarget(entity, d.RayDirection.ReadRootFrom, targets);
                if (dirEntity != Entity.Null && TransformLookup.HasComponent(dirEntity))
                    direction = math.rotate(TransformLookup[dirEntity].Rotation, math.forward());

                var remainingDistance = d.MaxDistance;
                var bounceCount = 0;

                var currentPos = origin;
                var currentDir = math.normalize(direction);

                var tier = TimelineDebugTier.Resolve(origin, Viewer, HasViewer);

                Drawer.Sphere(origin, 0.15f, Segments, OriginColor);
                if (tier >= DebugTier.Mid)
                    Drawer.Text32(origin + new float3(0f, 0.3f, 0f), (FixedString32Bytes)"Ricochet", TextColor, 10f);

                while (bounceCount <= d.MaxBounces && remainingDistance > 0)
                {
                    var rayColor = bounceCount % 2 == 0 ? RayColor1 : RayColor2;

                    var stepResult = PhysicsMath.StepRicochet(
                        currentPos, currentDir, remainingDistance,
                        d.RicochetMask, d.TerminalHitMask, d.MinGrazingAngle,
                        CollisionWorld, ColliderLookup);

                    if (!stepResult.HitFound)
                    {
                        var endPos = currentPos + currentDir * remainingDistance;
                        Drawer.Line(currentPos, endPos, rayColor);
                        if (tier == DebugTier.Close)
                            Drawer.Text32(endPos + new float3(0f, 0.2f, 0f), (FixedString32Bytes)"Max Range",
                                new Color(0.6f, 0.6f, 0.6f, 0.8f), 8f);
                        break;
                    }

                    remainingDistance -= stepResult.DistanceTraveled;

                    if (stepResult.IsTerminal)
                    {
                        Drawer.Line(currentPos, stepResult.HitPosition, rayColor);
                        Drawer.Sphere(stepResult.HitPosition, 0.2f, Segments, TerminalColor);

                        Drawer.Arrow(stepResult.HitPosition, stepResult.SurfaceNormal * 0.5f,
                            new Color(TerminalColor.r, TerminalColor.g, TerminalColor.b, 0.5f));

                        if (tier >= DebugTier.Mid)
                            Drawer.Text32(stepResult.HitPosition + new float3(0f, 0.4f, 0f),
                                (FixedString32Bytes)"Terminal Hit", TerminalColor, 12f);

                        if (d.HitConditionKey != 0)
                        {
                            var routeTarget = ResolveTarget(entity, d.HitRouteTo.ReadRootFrom, targets);
                            if (routeTarget != Entity.Null &&
                                TransformLookup.TryGetComponent(routeTarget, out var routeLtw))
                            {
                                Drawer.Line(stepResult.HitPosition, routeLtw.Position,
                                    new Color(TerminalColor.r, TerminalColor.g, TerminalColor.b, 0.3f));
                                if (tier == DebugTier.Close)
                                    Drawer.Text32(routeLtw.Position + new float3(0f, 0.5f, 0f),
                                        (FixedString32Bytes)"Route Target", TextColor, 8f);
                            }
                        }

                        break;
                    }

                    if (stepResult.IsRicochet)
                    {
                        Drawer.Line(currentPos, stepResult.HitPosition, rayColor);
                        Drawer.Sphere(stepResult.HitPosition, 0.1f, Segments, rayColor);

                        if (tier == DebugTier.Close)
                        {
                            var bounceLabel = new FixedString32Bytes();
                            bounceLabel.Append((FixedString32Bytes)"Bounce ");
                            bounceLabel.Append(bounceCount + 1);
                            Drawer.Text32(stepResult.HitPosition + new float3(0f, 0.2f, 0f), bounceLabel, rayColor,
                                10f);
                        }

                        Drawer.Arrow(stepResult.HitPosition, stepResult.SurfaceNormal * 0.4f,
                            new Color(0.8f, 0.8f, 0.8f, 0.3f));

                        currentDir = currentDir - 2f * math.dot(currentDir, stepResult.SurfaceNormal) *
                            stepResult.SurfaceNormal;
                        currentPos = stepResult.HitPosition + currentDir * 0.01f;
                        bounceCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (tier == DebugTier.Close)
                {
                    var readout = new FixedString128Bytes();
                    readout.Append((FixedString32Bytes)"bounces ");
                    readout.Append(bounceCount);
                    readout.Append((FixedString32Bytes)" / ");
                    readout.Append(d.MaxBounces);
                    readout.Append((FixedString32Bytes)"  rem ");
                    readout.Append(remainingDistance);
                    Drawer.Text128(origin + new float3(0f, 0.6f, 0f), readout, TextColor, 10f);
                }
            }

            private Entity ResolveTarget(Entity self, Target target, in Targets targets)
            {
                if (target == Target.None) return self;
                var baseTarget = targets.Get(target, self);
                return baseTarget != Entity.Null ? baseTarget : self;
            }
        }
    }
}
#endif