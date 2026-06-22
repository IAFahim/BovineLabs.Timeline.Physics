#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.PhysicsStates;
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

                DrawBox(ref Drawer, center, rot, config.DebugExtents, col);

                if (active && tier >= DebugTier.Mid)
                {
                    Drawer.Line(state.PrevPosition, ltw.Position, PathColor);

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