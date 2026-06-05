using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Teleports;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR || BL_DEBUG
namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters should be documented",
        Justification = "Using see cref")]
    public static class TeleportDebugSystem
    {
        [ConfigVar("teleportgizmo.draw-enabled", false, "Enable the teleport gizmo drawer.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("teleportgizmo.patch-color", 0.2f, 0.92f, 0.6f, 0.85f, "Accent for the spherical patch boundary.")]
        public static readonly SharedStatic<Color> PatchColor = SharedStatic<Color>.GetOrCreate<Tags.PatchColor>();

        [ConfigVar("teleportgizmo.radius-color", 0.9f, 0.7f, 0.2f, 0.4f, "Accent for the radius circle.")]
        public static readonly SharedStatic<Color> RadiusColor = SharedStatic<Color>.GetOrCreate<Tags.RadiusColor>();

        [ConfigVar("teleportgizmo.clearance-color", 0.95f, 0.4f, 0.3f, 0.6f, "Accent for the clearance sphere.")]
        public static readonly SharedStatic<Color> ClearanceColor =
            SharedStatic<Color>.GetOrCreate<Tags.ClearanceColor>();

        [ConfigVar("teleportgizmo.reference-color", 0.4f, 0.8f, 1f, 0.7f, "Accent for reference frame arrows.")]
        public static readonly SharedStatic<Color> ReferenceColor =
            SharedStatic<Color>.GetOrCreate<Tags.ReferenceColor>();

        [ConfigVar("teleportgizmo.los-color", 0.3f, 1f, 0.5f, 0.5f, "Accent for line of sight line.")]
        public static readonly SharedStatic<Color> LosColor = SharedStatic<Color>.GetOrCreate<Tags.LosColor>();

        [ConfigVar("teleportgizmo.text-color", 1f, 1f, 1f, 0.9f, "Color for value labels.")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        [ConfigVar("teleportgizmo.segments", 24, "Arc segment count.")]
        public static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<Tags.Segments>();

        [ConfigVar("teleportgizmo.verbose", false, "Show verbose text labels.")]
        public static readonly SharedStatic<bool> Verbose = SharedStatic<bool>.GetOrCreate<Tags.Verbose>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct PatchColor
            {
            }

            public struct RadiusColor
            {
            }

            public struct ClearanceColor
            {
            }

            public struct ReferenceColor
            {
            }

            public struct LosColor
            {
            }

            public struct TextColor
            {
            }

            public struct Segments
            {
            }

            public struct Verbose
            {
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    [BurstCompile]
    public partial struct PhysicsTeleportGizmoSystem : ISystem
    {
        private UnsafeComponentLookup<LocalToWorld> _localToWorldLookup;
        private UnsafeComponentLookup<Targets> _targetsLookup;
        private UnsafeComponentLookup<EntityLinkSource> _linkSourceLookup;
        private UnsafeBufferLookup<EntityLinkEntry> _linkLookup;

        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _localToWorldLookup = state.GetUnsafeComponentLookup<LocalToWorld>(true);
            _targetsLookup = state.GetUnsafeComponentLookup<Targets>(true);
            _linkSourceLookup = state.GetUnsafeComponentLookup<EntityLinkSource>(true);
            _linkLookup = state.GetUnsafeBufferLookup<EntityLinkEntry>(true);

            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTeleportAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localToWorldLookup.Update(ref state);
            _targetsLookup.Update(ref state);
            _linkSourceLookup.Update(ref state);
            _linkLookup.Update(ref state);

            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTeleportGizmoSystem>(
                    ref state, TeleportDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawTeleportJob
            {
                Drawer = drawer,
                Segments = math.clamp(TeleportDebugSystem.Segments.Data, 8, 64),
                PatchColor = TeleportDebugSystem.PatchColor.Data,
                RadiusColor = TeleportDebugSystem.RadiusColor.Data,
                ClearanceColor = TeleportDebugSystem.ClearanceColor.Data,
                ReferenceColor = TeleportDebugSystem.ReferenceColor.Data,
                LosColor = TeleportDebugSystem.LosColor.Data,
                TextColor = TeleportDebugSystem.TextColor.Data,
                Verbose = TeleportDebugSystem.Verbose.Data,
                TransformLookup = _localToWorldLookup,
                TargetsLookup = _targetsLookup,
                LinkSources = _linkSourceLookup,
                Links = _linkLookup
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(ClipActive))]
        private partial struct DrawTeleportJob : IJobEntity
        {
            public Drawer Drawer;
            public int Segments;
            public Color PatchColor;
            public Color RadiusColor;
            public Color ClearanceColor;
            public Color ReferenceColor;
            public Color LosColor;
            public Color TextColor;
            public bool Verbose;
            [ReadOnly] public UnsafeComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public UnsafeComponentLookup<EntityLinkSource> LinkSources;
            [ReadOnly] public UnsafeBufferLookup<EntityLinkEntry> Links;

            private void Execute(Entity entity, in TrackBinding binding, in PhysicsTeleportAnimated animated)
            {
                if (!TransformLookup.TryGetComponent(binding.Value, out var trackLtw))
                    return;

                var d = animated.AuthoredData;

                var frame = TeleportResolver.Resolve(
                    entity, d, trackLtw.Position,
                    TransformLookup, TargetsLookup, LinkSources, Links);

                Drawer.Circle(frame.LandingPosition, new float3(0f, d.Radius, 0f), RadiusColor);
                Drawer.Sphere(frame.LandingPosition, 0.15f, Segments, ReferenceColor);

                DrawPatchBoundary(frame.LandingPosition, d.Radius, frame.ReferenceRotation,
                    d.AzimuthCenter, d.AzimuthHalfRange,
                    d.ElevationCenter, d.ElevationHalfRange);

                Drawer.Arrow(frame.LandingPosition, math.mul(frame.ReferenceRotation, math.forward()) * 1.5f,
                    ReferenceColor);

                Drawer.Line(frame.AzimuthPosition, frame.LandingPosition, new Color(0.5f, 0.5f, 0.5f, 0.3f));

                if (d.RequireLineOfSight)
                {
                    var losStart = frame.TeleportedPosition + new float3(0f, d.LineOfSightOffset, 0f);
                    var losEnd = frame.LandingPosition + new float3(0f, d.LineOfSightOffset, 0f);
                    Drawer.Line(losStart, losEnd, LosColor);
                    if (Verbose)
                        Drawer.Text32((losStart + losEnd) * 0.5f + new float3(0f, 0.25f, 0f), "LOS Check", LosColor,
                            8f);
                }

                Drawer.Arrow(frame.LandingPosition + new float3(0f, 0.2f, 0f),
                    math.mul(frame.FacingRotation, math.forward()) * 1.25f,
                    new Color(ReferenceColor.r, ReferenceColor.g, ReferenceColor.b, 0.7f));

                if (Verbose)
                {
                    Drawer.Text32(frame.LandingPosition + new float3(0f, 0.5f, 0f), $"r={d.Radius:G2}", RadiusColor,
                        10f);
                    Drawer.Text32(frame.LandingPosition + new float3(d.Radius + 0.2f, 1f, 0f),
                        $"Az {math.degrees(d.AzimuthCenter):G0}° ±{math.degrees(d.AzimuthHalfRange):G0}°", TextColor,
                        8f);
                    Drawer.Text32(frame.LandingPosition + new float3(d.Radius + 0.2f, 0.6f, 0f),
                        $"El {math.degrees(d.ElevationCenter):G0}° ±{math.degrees(d.ElevationHalfRange):G0}°",
                        TextColor, 8f);

                    var facingLabel = d.FacingMode switch
                    {
                        TeleportFacingMode.FaceTarget => "Face Target",
                        TeleportFacingMode.FaceAway => "Face Away",
                        TeleportFacingMode.PreserveCurrent => "Preserve",
                        TeleportFacingMode.MatchTarget => "Match Target",
                        _ => "?"
                    };
                    Drawer.Text32(frame.LandingPosition + new float3(d.Radius + 0.2f, 0.2f, 0f), facingLabel, TextColor,
                        8f);
                }
            }

            private void DrawPatchBoundary(float3 origin, float radius, quaternion referenceRot,
                float azCenter, float azHalf, float elCenter, float elHalf)
            {
                var azMin = azCenter - azHalf;
                var azMax = azCenter + azHalf;
                var elMin = elCenter - elHalf;
                var elMax = elCenter + elHalf;

                DrawArcSweepAzimuth(origin, radius, referenceRot, azMin, azMax, elMin);
                DrawArcSweepAzimuth(origin, radius, referenceRot, azMin, azMax, elMax);

                var elSegments = math.max(Segments / 4, 4);
                DrawArcSweepElevation(origin, radius, referenceRot, azMin, elMin, elMax, elSegments);
                DrawArcSweepElevation(origin, radius, referenceRot, azMax, elMin, elMax, elSegments);
            }

            private void DrawArcSweepAzimuth(float3 origin, float radius, quaternion referenceRot,
                float azMin, float azMax, float elevation)
            {
                var step = (azMax - azMin) / Segments;
                var prev = origin + math.rotate(referenceRot, SphericalToCartesian(azMin, elevation)) * radius;
                for (var i = 1; i <= Segments; i++)
                {
                    var curr = origin + math.rotate(referenceRot, SphericalToCartesian(azMin + step * i, elevation)) *
                        radius;
                    Drawer.Line(prev, curr, PatchColor);
                    prev = curr;
                }
            }

            private void DrawArcSweepElevation(float3 origin, float radius, quaternion referenceRot,
                float azimuth, float elMin, float elMax, int steps)
            {
                var step = (elMax - elMin) / steps;
                var prev = origin + math.rotate(referenceRot, SphericalToCartesian(azimuth, elMin)) * radius;
                for (var i = 1; i <= steps; i++)
                {
                    var curr = origin + math.rotate(referenceRot, SphericalToCartesian(azimuth, elMin + step * i)) *
                        radius;
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