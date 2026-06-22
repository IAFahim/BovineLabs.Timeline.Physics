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
    /// <summary>
    /// Quill visualizer for the Swept Trigger: draws the swept capsule volume, the prev-&gt;cur sweep path,
    /// a marker on each currently-overlapped entity, and the live Enter/Stay/Exit labels. Enable via the
    /// config var <c>sweptgizmo.draw-enabled</c> (or the ConfigVars window) and press Play.
    /// </summary>
    [Configurable]
    public static class SweptTriggerDebugConfig
    {
        [ConfigVar("sweptgizmo.draw-enabled", false, "Draw the swept-trigger capsule, sweep path and hits.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("sweptgizmo.active-color", 0.2f, 0.95f, 0.3f, 0.8f, "Swept capsule colour while sweeping (green).")]
        public static readonly SharedStatic<Color> ActiveColor = SharedStatic<Color>.GetOrCreate<Tags.ActiveColor>();

        [ConfigVar("sweptgizmo.idle-color", 0.3f, 0.6f, 1f, 0.55f, "Swept capsule colour while idle (blue) — shows placement when not swinging.")]
        public static readonly SharedStatic<Color> IdleColor = SharedStatic<Color>.GetOrCreate<Tags.IdleColor>();

        [ConfigVar("sweptgizmo.path-color", 0.95f, 0.85f, 0.2f, 0.9f, "Sweep path colour (yellow).")]
        public static readonly SharedStatic<Color> PathColor = SharedStatic<Color>.GetOrCreate<Tags.PathColor>();

        [ConfigVar("sweptgizmo.hit-color", 0.95f, 0.25f, 0.25f, 0.95f, "Hit marker colour (red).")]
        public static readonly SharedStatic<Color> HitColor = SharedStatic<Color>.GetOrCreate<Tags.HitColor>();

        [ConfigVar("sweptgizmo.text-color", 1f, 1f, 1f, 0.95f, "Label colour.")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct ActiveColor { }
            public struct IdleColor { }
            public struct PathColor { }
            public struct HitColor { }
            public struct TextColor { }
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
            {
                return;
            }

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
                TextColor = SweptTriggerDebugConfig.TextColor.Data,
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;

            [ReadOnly]
            public ComponentLookup<LocalToWorld> LtwLookup;

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

                // ALWAYS draw the capsule volume (dim blue when idle, bright green while sweeping) so a designer
                // can see the hit volume's PLACEMENT at any time, not just during the brief active swing window.
                // The noisy extras (sweep path, hit markers, labels) stay gated on `active` below to avoid idle
                // clutter — so an idle weapon shows just its volume outline, an active one shows the full detail.
                var col = active ? ActiveColor : IdleColor;

                // Draw EXACTLY the volume the swept query tests: SweptTriggerSystem casts the capsule via
                // RigidTransform(ltw.Rotation, ltw.Position) with QueryColliderScale = 1 — it ignores the
                // source's (often bone-inherited) world SCALE. Transforming the endpoints through the full
                // ltw.Value instead would scale the drawn capsule and make the play-mode volume jump away from
                // both the query and the unscaled author gizmo (the "before/after switch on Play"). Use
                // position + rotation only so edit gizmo, runtime debug, and the actual hit test all coincide.
                // The capsule's own hemispherical caps already enclose v0/v1, so no extra end spheres are drawn.
                var pos = ltw.Position;
                var rot = ltw.Rotation;
                var v0 = pos + math.rotate(rot, config.Vertex0);
                var v1 = pos + math.rotate(rot, config.Vertex1);
                var center = (v0 + v1) * 0.5f;
                var height = math.distance(v0, v1) + (2f * config.Radius);

                var tier = TimelineDebugTier.Resolve(center, this.Viewer, this.HasViewer);

                // Far: what the system does — the swept capsule volume.
                this.Drawer.Capsule(center, rot, height, config.Radius, 12, col);

                if (active && tier >= DebugTier.Mid)
                {
                    // Mid: the sweep path + markers on overlapped entities + a label (active sources only).
                    this.Drawer.Line(state.PrevPosition, ltw.Position, this.PathColor);

                    for (var i = 0; i < hits.Length; i++)
                    {
                        var e = hits[i].Value;
                        if (this.LtwLookup.HasComponent(e))
                        {
                            var hp = this.LtwLookup[e].Position;
                            this.Drawer.Sphere(hp, 0.25f, 10, this.HitColor);
                            this.Drawer.Line(center, hp, this.HitColor);
                        }
                    }

                    this.Drawer.Text32(ltw.Position + new float3(0f, 1.2f, 0f), (FixedString32Bytes)"Swept",
                        this.TextColor, 10f);
                }

                if (!active || tier != DebugTier.Close)
                {
                    return;
                }

                // Close: live Enter/Stay/Exit labels + hit count (active sources only).
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
                    {
                        label.Append(Enter);
                    }
                    else if (s == StatefulEventState.Stay)
                    {
                        label.Append(Stay);
                    }
                    else if (s == StatefulEventState.Exit)
                    {
                        label.Append(Exit);
                    }
                }

                this.Drawer.Text128(ltw.Position + new float3(0f, 1.5f, 0f), label, this.TextColor, 10f);
            }
        }
    }
}
#endif
