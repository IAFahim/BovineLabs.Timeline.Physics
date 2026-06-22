using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class DragDebugSystem
    {
        [ConfigVar("draggizmo.draw-enabled", false, "Enable the drag gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("draggizmo.trail-color", 0.6f, 0.4f, 0.2f, 0.8f, "Color for predicted drag trail (Warm Brown)")]
        public static readonly SharedStatic<Color> TrailColor = SharedStatic<Color>.GetOrCreate<Tags.TrailColor>();

        [ConfigVar("draggizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        [ConfigVar("draggizmo.segments", 16, "Number of segments for spheres and arcs")]
        public static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<Tags.Segments>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct TrailColor
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
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct PhysicsDragGizmoSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsDragAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsDragGizmoSystem>(
                    ref state, DragDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                Segments = math.clamp(DragDebugSystem.Segments.Data, 8, 32),
                TrailColor = DragDebugSystem.TrailColor.Data,
                TextColor = DragDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                VelocityLookup = SystemAPI.GetComponentLookup<PhysicsVelocity>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public int Segments;
            public Color TrailColor;
            public Color TextColor;

            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;

            private float3 GetAntiJitterPosition(Entity e, float3 fallback)
            {
                if (LocalTransformLookup.HasComponent(e) && !ParentLookup.HasComponent(e))
                    return LocalTransformLookup[e].Position;

                return fallback;
            }

            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsDragAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = GetAntiJitterPosition(target, ltw.Position);

                var hasVel = VelocityLookup.TryGetComponent(target, out var vel);
                var linVel = hasVel ? vel.Linear : float3.zero;

                var tier = TimelineDebugTier.Resolve(pos, Viewer, HasViewer);

                if (math.lengthsq(linVel) > 0.01f)
                    Drawer.Arrow(pos, -math.normalize(linVel) * 0.75f, TrailColor);
                else
                    Drawer.Sphere(pos, 0.1f, Segments, TrailColor);

                if (tier >= DebugTier.Mid)
                    Drawer.Text32(pos + new float3(0, 0.4f, 0), (FixedString32Bytes)"Drag", TextColor, 10f);

                if (tier == DebugTier.Close)
                {
                    var readout = new FixedString128Bytes();
                    readout.Append((FixedString32Bytes)"lin ");
                    readout.Append(d.Linear);
                    readout.Append((FixedString32Bytes)"  ang ");
                    readout.Append(d.Angular);
                    readout.Append((FixedString32Bytes)"  v ");
                    readout.Append(math.length(linVel));
                    Drawer.Text128(pos + new float3(0, 0.2f, 0), readout, TextColor, 10f);
                }

                if (tier == DebugTier.Close && math.lengthsq(linVel) > 0.01f && d.Linear > 0f)
                {
                    var currentPos = pos;
                    var currentVel = linVel;
                    const float dt = 0.05f;
                    var steps = (int)(2f / dt);
                    var alpha = TrailColor.a;

                    for (var i = 0; i < steps; i++)
                    {
                        currentVel *= math.exp(-d.Linear * dt);
                        var nextPos = currentPos + currentVel * dt;

                        var segmentColor = TrailColor;
                        segmentColor.a = alpha;
                        Drawer.Line(currentPos, nextPos, segmentColor);

                        currentPos = nextPos;
                        alpha *= 0.95f;

                        if (math.lengthsq(currentVel) < 0.01f)
                        {
                            Drawer.Sphere(currentPos, 0.1f, Segments, segmentColor);
                            break;
                        }
                    }
                }
            }
        }
    }
}
#endif