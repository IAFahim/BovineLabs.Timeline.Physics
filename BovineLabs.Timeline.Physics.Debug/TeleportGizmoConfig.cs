#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class TeleportGizmoConfig
    {
        [ConfigVar("teleportgizmo.force-draw", false, "Enable the teleport gizmo drawer.")]
        internal static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<EnabledType>();

        [ConfigVar("teleportgizmo.patch-color", 0.2f, 0.92f, 0.6f, 0.85f, "Accent for the spherical patch boundary.")]
        internal static readonly SharedStatic<Color> PatchColor = SharedStatic<Color>.GetOrCreate<PatchColorType>();

        [ConfigVar("teleportgizmo.radius-color", 0.9f, 0.7f, 0.2f, 0.4f, "Accent for the radius circle.")]
        internal static readonly SharedStatic<Color> RadiusColor = SharedStatic<Color>.GetOrCreate<RadiusColorType>();

        [ConfigVar("teleportgizmo.clearance-color", 0.95f, 0.4f, 0.3f, 0.6f, "Accent for the clearance sphere.")]
        internal static readonly SharedStatic<Color> ClearanceColor = SharedStatic<Color>.GetOrCreate<ClearanceColorType>();

        [ConfigVar("teleportgizmo.segments", 24, "Arc segment count.")]
        internal static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<SegmentsType>();

        private struct EnabledType { }
        private struct PatchColorType { }
        private struct RadiusColorType { }
        private struct ClearanceColorType { }
        private struct SegmentsType { }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    [BurstCompile]
    public partial struct PhysicsTeleportGizmoSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<PhysicsTeleportAnimated, LocalToWorld>()
                    .Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<DrawSystem.Singleton>()) return;
            ref var drawSystem = ref SystemAPI.GetSingletonRW<DrawSystem.Singleton>().ValueRW;

            Drawer drawer;
            if (!TeleportGizmoConfig.Enabled.Data)
            {
                drawer = drawSystem.CreateDrawer<PhysicsTeleportGizmoSystem>();
                if (!drawer.IsEnabled) return;
            }
            else
            {
                drawer = drawSystem.CreateDrawer();
            }

            state.Dependency = new DrawTeleportPatchJob
            {
                Drawer         = drawer,
                Segments       = math.clamp(TeleportGizmoConfig.Segments.Data, 8, 64),
                PatchColor     = TeleportGizmoConfig.PatchColor.Data,
                RadiusColor    = TeleportGizmoConfig.RadiusColor.Data,
                ClearanceColor = TeleportGizmoConfig.ClearanceColor.Data,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawTeleportPatchJob : IJobEntity
        {
            public Drawer Drawer;
            public int Segments;
            public Color PatchColor;
            public Color RadiusColor;
            public Color ClearanceColor;

            private void Execute(in PhysicsTeleportAnimated animated, in LocalToWorld ltw)
            {
                ref readonly var d = ref animated.AuthoredData;
                var origin = ltw.Position;
                var r      = d.Radius;

                Drawer.Circle(origin, new float3(0f, r, 0f), RadiusColor);

                DrawPatchBoundary(origin, r,
                    d.AzimuthCenter, d.AzimuthHalfRange,
                    d.ElevationCenter, d.ElevationHalfRange);

                var centerDir   = SphericalToCartesian(d.AzimuthCenter, d.ElevationCenter);
                var centerPoint = origin + centerDir * r;
                Drawer.Line(origin, centerPoint, PatchColor);
                Drawer.Sphere(centerPoint, d.ClearanceRadius, 8, ClearanceColor);
            }

            private void DrawPatchBoundary(float3 origin, float radius,
                float azCenter, float azHalf, float elCenter, float elHalf)
            {
                var azMin = azCenter - azHalf;
                var azMax = azCenter + azHalf;
                var elMin = elCenter - elHalf;
                var elMax = elCenter + elHalf;

                DrawArcSweepAzimuth(origin, radius, azMin, azMax, elMin);
                DrawArcSweepAzimuth(origin, radius, azMin, azMax, elMax);

                var elSegments = math.max(Segments / 4, 4);
                DrawArcSweepElevation(origin, radius, azMin, elMin, elMax, elSegments);
                DrawArcSweepElevation(origin, radius, azMax, elMin, elMax, elSegments);
            }

            private void DrawArcSweepAzimuth(float3 origin, float radius,
                float azMin, float azMax, float elevation)
            {
                var step = (azMax - azMin) / Segments;
                var prev = origin + SphericalToCartesian(azMin, elevation) * radius;
                for (var i = 1; i <= Segments; i++)
                {
                    var curr = origin + SphericalToCartesian(azMin + step * i, elevation) * radius;
                    Drawer.Line(prev, curr, PatchColor);
                    prev = curr;
                }
            }

            private void DrawArcSweepElevation(float3 origin, float radius,
                float azimuth, float elMin, float elMax, int steps)
            {
                var step = (elMax - elMin) / steps;
                var prev = origin + SphericalToCartesian(azimuth, elMin) * radius;
                for (var i = 1; i <= steps; i++)
                {
                    var curr = origin + SphericalToCartesian(azimuth, elMin + step * i) * radius;
                    Drawer.Line(prev, curr, PatchColor);
                    prev = curr;
                }
            }

            private static float3 SphericalToCartesian(float azimuth, float elevation)
            {
                var cosEl = math.cos(elevation);
                return new float3(
                    math.sin(azimuth) * cosEl,
                    math.sin(elevation),
                    math.cos(azimuth) * cosEl);
            }
        }
    }
}
#endif