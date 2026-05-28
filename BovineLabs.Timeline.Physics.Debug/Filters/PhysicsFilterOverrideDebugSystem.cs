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
    public static class FilterOverrideDebugSystem
    {
        [ConfigVar("filtergizmo.draw-enabled", true, "Enable the filter override gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("filtergizmo.ring-color", 0.8f, 0.2f, 0.2f, 0.9f, "Color for filter active ring (Red alert)")]
        public static readonly SharedStatic<Color> RingColor = SharedStatic<Color>.GetOrCreate<Tags.RingColor>();

        [ConfigVar("filtergizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct RingColor { }
            public struct TextColor { }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsFilterOverrideGizmoSystem : ISystem
    {
        private EntityQuery _query;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsFilterOverrideAnimated, ClipActive>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsFilterOverrideGizmoSystem>(
                    ref state, FilterOverrideDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer     = drawer,
                RingColor  = FilterOverrideDebugSystem.RingColor.Data,
                TextColor  = FilterOverrideDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public Color RingColor;
            public Color TextColor;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsFilterOverrideAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = ltw.Position;
                
                // Ring at the entity's feet (approximate base, y - 0.5 or just center if not known)
                var groundPos = pos - new float3(0f, 0.5f, 0f);
                Drawer.Circle(groundPos, new float3(0f, 0.7f, 0f), RingColor);
                Drawer.Circle(groundPos, new float3(0f, 0.65f, 0f), RingColor); // double ring to make it bright

                Drawer.Text32(pos + new float3(0f, 0.4f, 0f), $"Belongs: 0x{d.BelongsToOverride:X8}", TextColor, 10f);
                Drawer.Text32(pos + new float3(0f, 0.2f, 0f), $"Collides: 0x{d.CollidesWithOverride:X8}", TextColor, 10f);
            }
        }
    }
}
#endif
