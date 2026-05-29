#if UNITY_EDITOR || BL_DEBUG
using System.Diagnostics.CodeAnalysis;
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
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
    public static class GravityOverrideDebugSystem
    {
        [ConfigVar("gravitygizmo.draw-enabled", false, "Enable the gravity override gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("gravitygizmo.arrow-color", 0.5f, 0.3f, 0.8f, 0.85f, "Color for gravity arrow (Muted Purple)")]
        public static readonly SharedStatic<Color> ArrowColor = SharedStatic<Color>.GetOrCreate<Tags.ArrowColor>();

        [ConfigVar("gravitygizmo.zero-g-color", 0.8f, 0.8f, 0.8f, 0.9f, "Color for zero-G marker")]
        public static readonly SharedStatic<Color> ZeroGColor = SharedStatic<Color>.GetOrCreate<Tags.ZeroGColor>();

        [ConfigVar("gravitygizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct ArrowColor { }
            public struct ZeroGColor { }
            public struct TextColor { }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsGravityOverrideGizmoSystem : ISystem
    {
        private EntityQuery _query;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsGravityOverrideAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsGravityOverrideGizmoSystem>(
                    ref state, GravityOverrideDebugSystem.Enabled.Data, out var drawer))
                return;

            var worldGravity = new float3(0, -9.81f, 0);
            if (SystemAPI.HasSingleton<PhysicsStep>())
                worldGravity = SystemAPI.GetSingleton<PhysicsStep>().Gravity;

            state.Dependency = new DrawJob
            {
                Drawer       = drawer,
                WorldGravity = worldGravity,
                ArrowColor   = GravityOverrideDebugSystem.ArrowColor.Data,
                ZeroGColor   = GravityOverrideDebugSystem.ZeroGColor.Data,
                TextColor    = GravityOverrideDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 WorldGravity;
            public Color ArrowColor;
            public Color ZeroGColor;
            public Color TextColor;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsGravityOverrideAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = ltw.Position;
                var gScale = d.GravityScale;

                if (math.abs(gScale) < 0.001f)
                {
                    Drawer.Text32(pos + new float3(0f, 0.5f, 0f), "g×0", ZeroGColor, 12f);
                    Drawer.Circle(pos + new float3(0f, 0.5f, 0f), new float3(0f, 0.15f, 0f), ZeroGColor);
                }
                else
                {
                    var gVec = WorldGravity * gScale;
                    var arrowLen = 1f; 
                    if (math.lengthsq(WorldGravity) > 0.01f)
                        arrowLen = math.length(gVec) / math.length(WorldGravity);

                    var dir = math.normalize(gVec);
                    Drawer.Arrow(pos, dir * arrowLen, ArrowColor);
                    
                    Drawer.Text32(pos + dir * arrowLen + new float3(0, 0.3f, 0), $"g×{gScale:G2}", TextColor, 10f);
                }
            }
        }
    }
}
#endif
