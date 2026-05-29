#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using BovineLabs.Core;
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
    public static class VelocityClampDebugSystem
    {
        [ConfigVar("clampgizmo.draw-enabled", true, "Enable the velocity clamp gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("clampgizmo.safe-color", 0.3f, 0.8f, 0.4f, 0.8f, "Color for safe speed (Greenish)")]
        public static readonly SharedStatic<Color> SafeColor = SharedStatic<Color>.GetOrCreate<Tags.SafeColor>();

        [ConfigVar("clampgizmo.warn-color", 0.9f, 0.7f, 0.1f, 0.8f, "Color for approaching limit (Amber)")]
        public static readonly SharedStatic<Color> WarnColor = SharedStatic<Color>.GetOrCreate<Tags.WarnColor>();

        [ConfigVar("clampgizmo.limit-color", 1.0f, 0.2f, 0.2f, 0.9f, "Color for hitting limit (Red)")]
        public static readonly SharedStatic<Color> LimitColor = SharedStatic<Color>.GetOrCreate<Tags.LimitColor>();

        [ConfigVar("clampgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();
        
        [ConfigVar("clampgizmo.segments", 16, "Number of segments for spheres and arcs")]
        public static readonly SharedStatic<int> Segments = SharedStatic<int>.GetOrCreate<Tags.Segments>();

        private struct Tags
        {
            public struct Enabled { }
            public struct SafeColor { }
            public struct WarnColor { }
            public struct LimitColor { }
            public struct TextColor { }
            public struct Segments { }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsVelocityClampGizmoSystem : ISystem
    {
        private EntityQuery _query;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsVelocityClampAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsVelocityClampGizmoSystem>(
                    ref state, VelocityClampDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer     = drawer,
                Segments   = math.clamp(VelocityClampDebugSystem.Segments.Data, 8, 32),
                SafeColor  = VelocityClampDebugSystem.SafeColor.Data,
                WarnColor  = VelocityClampDebugSystem.WarnColor.Data,
                LimitColor = VelocityClampDebugSystem.LimitColor.Data,
                TextColor  = VelocityClampDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                VelocityLookup  = SystemAPI.GetComponentLookup<PhysicsVelocity>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public int Segments;
            public Color SafeColor;
            public Color WarnColor;
            public Color LimitColor;
            public Color TextColor;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;

            private float3 GetAntiJitterPosition(Entity e, float3 fallback)
            {
                if (LocalTransformLookup.HasComponent(e) && !ParentLookup.HasComponent(e))
                {
                    return LocalTransformLookup[e].Position;
                }
                return fallback;
            }

            [ReadOnly] public ComponentLookup<PhysicsVelocity> VelocityLookup;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsVelocityClampAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = GetAntiJitterPosition(target, ltw.Position);
                
                var hasVel = VelocityLookup.TryGetComponent(target, out var vel);
                var linSpd = hasVel ? math.length(vel.Linear) : 0f;
                var angSpd = hasVel ? math.length(vel.Angular) : 0f;

                if (d.MaxLinearSpeed > 0.001f)
                {
                    var limit = d.MaxLinearSpeed;
                    if (limit > 0.001f)
                    {
                        var ratio = math.saturate(linSpd / limit);
                        var col = EvaluateColor(ratio);
                        
                        var visualRadius = limit * 0.1f;
                        visualRadius = math.clamp(visualRadius, 0.5f, 5f);
                        
                        Drawer.Sphere(pos, visualRadius, Segments, col);
                        
                        if (hasVel && linSpd > 0.01f)
                        {
                            var velDir = math.normalize(vel.Linear);
                            Drawer.Arrow(pos, velDir * (visualRadius * ratio), col);
                        }

                        Drawer.Text32(pos + new float3(0, visualRadius + 0.3f, 0), $"max: {limit:G2} m/s", TextColor, 10f);
                    }
                }
                
                if (d.MaxAngularSpeed > 0.001f)
                {
                    var limit = d.MaxAngularSpeed;
                    if (limit > 0.001f)
                    {
                        var ratio = math.saturate(angSpd / limit);
                        var col = EvaluateColor(ratio);
                        
                        var visualRadius = limit * 0.2f;
                        visualRadius = math.clamp(visualRadius, 0.5f, 5f);
                        
                        Drawer.Circle(pos, new float3(0f, visualRadius, 0f), col);
                        Drawer.Text32(pos + new float3(visualRadius + 0.2f, 0, 0), $"amax: {limit:G2} rad/s", TextColor, 10f);
                    }
                }
            }
            
            private Color EvaluateColor(float ratio)
            {
                if (ratio < 0.7f) return SafeColor;
                if (ratio < 0.95f) return Color.Lerp(SafeColor, WarnColor, (ratio - 0.7f) / 0.25f);
                return Color.Lerp(WarnColor, LimitColor, (ratio - 0.95f) / 0.05f);
            }
        }
    }
}
#endif
