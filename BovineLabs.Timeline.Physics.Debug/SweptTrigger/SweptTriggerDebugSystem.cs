#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Nerve.PhysicsStates;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class SweptTriggerDebugConfig
    {
        [ConfigVar("sweptgizmo.draw-enabled", false, "Draw the swept-trigger capsule, sweep path and hits.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("sweptgizmo.active-color", 0.2f, 0.95f, 0.3f, 0.8f, "Swept capsule colour while sweeping (green).")]
        public static readonly SharedStatic<Color> ActiveColor = SharedStatic<Color>.GetOrCreate<Tags.ActiveColor>();

        [ConfigVar("sweptgizmo.idle-color", 0.3f, 0.6f, 1f, 0.55f,
            "Swept capsule colour while idle (blue) — shows placement when not swinging.")]
        public static readonly SharedStatic<Color> IdleColor = SharedStatic<Color>.GetOrCreate<Tags.IdleColor>();

        [ConfigVar("sweptgizmo.path-color", 0.95f, 0.85f, 0.2f, 0.9f, "Sweep path colour (yellow).")]
        public static readonly SharedStatic<Color> PathColor = SharedStatic<Color>.GetOrCreate<Tags.PathColor>();

        [ConfigVar("sweptgizmo.hit-color", 0.95f, 0.25f, 0.25f, 0.95f, "Hit marker colour (red).")]
        public static readonly SharedStatic<Color> HitColor = SharedStatic<Color>.GetOrCreate<Tags.HitColor>();

        [ConfigVar("sweptgizmo.text-color", 1f, 1f, 1f, 0.95f, "Label colour.")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct ActiveColor
            {
            }

            public struct IdleColor
            {
            }

            public struct PathColor
            {
            }

            public struct HitColor
            {
            }

            public struct TextColor
            {
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct SweptTriggerDebugSystem : ISystem
    {
        private ComponentLookup<LocalToWorld> _ltwLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            state.RequireForUpdate<SweptTriggerConfig>();
            _ltwLookup = state.GetComponentLookup<LocalToWorld>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<SweptTriggerDebugSystem>(
                    ref state, SweptTriggerDebugConfig.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            _ltwLookup.Update(ref state);

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                LtwLookup = _ltwLookup,
                ActiveColor = SweptTriggerDebugConfig.ActiveColor.Data,
                IdleColor = SweptTriggerDebugConfig.IdleColor.Data,
                PathColor = SweptTriggerDebugConfig.PathColor.Data,
                HitColor = SweptTriggerDebugConfig.HitColor.Data,
                TextColor = SweptTriggerDebugConfig.TextColor.Data
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;

            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;

            public Color ActiveColor;
            public Color IdleColor;
            public Color PathColor;
            public Color HitColor;
            public Color TextColor;

            private static readonly FixedString32Bytes Enter = "ENTER ";
            private static readonly FixedString32Bytes Stay = "STAY ";
            private static readonly FixedString32Bytes Exit = "EXIT ";

            private void Execute(
                Entity entity,
                in SweptTriggerConfig config,
                in SweptTriggerState state,
                in LocalToWorld ltw,
                in DynamicBuffer<SweptTriggerHit> hits,
                in DynamicBuffer<StatefulTriggerEvent> events)
            {
                var active = state.WasActive == 1;

                var col = active ? ActiveColor : IdleColor;

                var pos = ltw.Position;
                var rot = ltw.Rotation;
                var center = pos + math.rotate(rot, config.DebugCenter);

                var tier = TimelineDebugTier.Resolve(center, Viewer, HasViewer);

                // Draw the ACTUAL swept shape (sphere/capsule/box/cylinder), not just its AABB box — this is what
                // the system really casts. The cast itself was always shape-correct; only this gizmo was box-only.
                DrawCollider(ref Drawer, in config.Collider, pos, rot, config.DebugCenter, config.DebugExtents, col);

                if (active && tier >= DebugTier.Mid)
                {
                    Drawer.Line(state.PrevPosition, ltw.Position, PathColor);

                    // Ghost the real shape at the sweep START so the swept volume reads, not just the end pose.
                    DrawCollider(ref Drawer, in config.Collider, state.PrevPosition, state.PrevRotation,
                        config.DebugCenter, config.DebugExtents,
                        new Color(PathColor.r, PathColor.g, PathColor.b, PathColor.a * 0.5f));

                    for (var i = 0; i < hits.Length; i++)
                    {
                        var e = hits[i].Value;
                        if (LtwLookup.HasComponent(e))
                        {
                            var hp = LtwLookup[e].Position;
                            Drawer.Sphere(hp, 0.25f, 10, HitColor);
                            Drawer.Line(center, hp, HitColor);
                        }
                    }

                    Drawer.Text32(ltw.Position + new float3(0f, 1.2f, 0f), (FixedString32Bytes)"Swept",
                        TextColor, 10f);
                }

                if (!active || tier != DebugTier.Close) return;

                FixedString128Bytes label = default;
                label.Append('S');
                label.Append('W');
                label.Append('E');
                label.Append('P');
                label.Append('T');
                label.Append(active ? '*' : '-');
                label.Append(' ');
                label.Append('h');
                label.Append('=');
                label.Append(hits.Length);
                label.Append('\n');
                for (var i = 0; i < events.Length; i++)
                {
                    var s = events[i].State;
                    if (s == StatefulEventState.Enter)
                        label.Append(Enter);
                    else if (s == StatefulEventState.Stay)
                        label.Append(Stay);
                    else if (s == StatefulEventState.Exit) label.Append(Exit);
                }

                Drawer.Text128(ltw.Position + new float3(0f, 1.5f, 0f), label, TextColor, 10f);
            }

            // Reads the real collider blob and draws the matching Quill primitive. Geometry is in the collider's
            // local space (== the swept entity's space, since the shape authoring sits on the same object), so we
            // place it at the entity pose. Convex/mesh/compound have no cheap exact wireframe -> baked AABB box.
            private static unsafe void DrawCollider(ref Drawer drawer,
                in BlobAssetReference<Unity.Physics.Collider> blob, float3 pos, quaternion rot,
                float3 fallbackCenter, float3 fallbackExtents, Color color)
            {
                if (!blob.IsCreated)
                {
                    DrawBox(ref drawer, pos + math.rotate(rot, fallbackCenter), rot, fallbackExtents, color);
                    return;
                }

                var ptr = blob.GetUnsafePtr();
                switch (((Unity.Physics.Collider*)ptr)->Type)
                {
                    case Unity.Physics.ColliderType.Sphere:
                    {
                        var g = ((Unity.Physics.SphereCollider*)ptr)->Geometry;
                        drawer.Sphere(pos + math.rotate(rot, g.Center), g.Radius, 12, color);
                        break;
                    }

                    case Unity.Physics.ColliderType.Capsule:
                    {
                        var g = ((Unity.Physics.CapsuleCollider*)ptr)->Geometry;
                        DrawCapsule(ref drawer, pos, rot, g.Vertex0, g.Vertex1, g.Radius, color);
                        break;
                    }

                    case Unity.Physics.ColliderType.Box:
                    {
                        var g = ((Unity.Physics.BoxCollider*)ptr)->Geometry;
                        DrawBox(ref drawer, pos + math.rotate(rot, g.Center), math.mul(rot, g.Orientation), g.Size,
                            color);
                        break;
                    }

                    case Unity.Physics.ColliderType.Cylinder:
                    {
                        var g = ((Unity.Physics.CylinderCollider*)ptr)->Geometry;
                        // Unity.Physics cylinder axis is local +Z; Quill's cylinder faces up (+Y) on identity.
                        var axis = math.rotate(math.mul(rot, g.Orientation), new float3(0f, 0f, 1f));
                        drawer.Cylinder(pos + math.rotate(rot, g.Center), FromTo(new float3(0f, 1f, 0f), axis),
                            g.Height, g.Radius, 16, color);
                        break;
                    }

                    default:
                        // ConvexHull / Mesh / Compound — show the baked AABB box as an honest approximation.
                        DrawBox(ref drawer, pos + math.rotate(rot, fallbackCenter), rot, fallbackExtents, color);
                        break;
                }
            }

            // Quill capsule faces up (+Y) on identity and takes a full tip-to-tip height; convert the
            // Unity.Physics Vertex0/Vertex1/Radius form to that.
            private static void DrawCapsule(ref Drawer drawer, float3 pos, quaternion rot, float3 v0, float3 v1,
                float radius, Color color)
            {
                var c0 = pos + math.rotate(rot, v0);
                var c1 = pos + math.rotate(rot, v1);
                var center = (c0 + c1) * 0.5f;
                var axis = math.normalizesafe(c1 - c0, new float3(0f, 1f, 0f));
                var height = math.distance(c0, c1) + (2f * radius);
                drawer.Capsule(center, FromTo(new float3(0f, 1f, 0f), axis), height, radius, 12, color);
            }

            // Shortest-arc rotation taking `from` onto `to`.
            private static quaternion FromTo(float3 from, float3 to)
            {
                from = math.normalizesafe(from, new float3(0f, 1f, 0f));
                to = math.normalizesafe(to, new float3(0f, 1f, 0f));
                var d = math.dot(from, to);
                if (d >= 0.99999f)
                    return quaternion.identity;

                if (d <= -0.99999f)
                {
                    var ortho = math.abs(from.x) < 0.9f ? new float3(1f, 0f, 0f) : new float3(0f, 1f, 0f);
                    return quaternion.AxisAngle(math.normalize(math.cross(from, ortho)), math.PI);
                }

                var c = math.cross(from, to);
                return math.normalize(new quaternion(c.x, c.y, c.z, 1f + d));
            }

            private static void DrawBox(ref Drawer drawer, float3 center, quaternion rot, float3 size, Color color)
            {
                var h = size * 0.5f;
                var c0 = center + math.rotate(rot, new float3(-h.x, -h.y, -h.z));
                var c1 = center + math.rotate(rot, new float3(h.x, -h.y, -h.z));
                var c2 = center + math.rotate(rot, new float3(h.x, -h.y, h.z));
                var c3 = center + math.rotate(rot, new float3(-h.x, -h.y, h.z));
                var c4 = center + math.rotate(rot, new float3(-h.x, h.y, -h.z));
                var c5 = center + math.rotate(rot, new float3(h.x, h.y, -h.z));
                var c6 = center + math.rotate(rot, new float3(h.x, h.y, h.z));
                var c7 = center + math.rotate(rot, new float3(-h.x, h.y, h.z));

                drawer.Line(c0, c1, color);
                drawer.Line(c1, c2, color);
                drawer.Line(c2, c3, color);
                drawer.Line(c3, c0, color);
                drawer.Line(c4, c5, color);
                drawer.Line(c5, c6, color);
                drawer.Line(c6, c7, color);
                drawer.Line(c7, c4, color);
                drawer.Line(c0, c4, color);
                drawer.Line(c1, c5, color);
                drawer.Line(c2, c6, color);
                drawer.Line(c3, c7, color);
            }
        }
    }
}
#endif