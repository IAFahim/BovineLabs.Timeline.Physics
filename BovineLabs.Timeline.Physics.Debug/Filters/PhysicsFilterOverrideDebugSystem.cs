#if UNITY_EDITOR || BL_DEBUG

using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using BovineLabs.Timeline.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class FilterOverrideDebugSystem
    {
        [ConfigVar("filtergizmo.draw-enabled", false, "Enable the filter override gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("filtergizmo.ring-color", 0.8f, 0.2f, 0.2f, 0.9f, "Color for filter active ring (Red alert)")]
        public static readonly SharedStatic<Color> RingColor = SharedStatic<Color>.GetOrCreate<Tags.RingColor>();

        [ConfigVar("filtergizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct RingColor
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
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsFilterOverrideGizmoSystem>(
                    ref state, FilterOverrideDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                RingColor = FilterOverrideDebugSystem.RingColor.Data,
                TextColor = FilterOverrideDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public Color RingColor;
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

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsFilterOverrideAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = GetAntiJitterPosition(target, ltw.Position);

                var tier = TimelineDebugTier.Resolve(pos, Viewer, HasViewer);

                var groundPos = pos - new float3(0f, 0.5f, 0f);
                Drawer.Circle(groundPos, new float3(0f, 0.7f, 0f), RingColor);

                if (tier >= DebugTier.Mid)
                {
                    Drawer.Circle(groundPos, new float3(0f, 0.65f, 0f), RingColor);
                    Drawer.Text32(pos + new float3(0f, 0.4f, 0f), (FixedString32Bytes)"Filter", TextColor, 10f);
                }

                if (tier == DebugTier.Close)
                {
                    var belongs = new FixedString128Bytes();
                    belongs.Append((FixedString32Bytes)"belongs 0x");
                    AppendHex8(ref belongs, d.BelongsToOverride);
                    Drawer.Text128(pos + new float3(0f, 0.2f, 0f), belongs, TextColor, 10f);

                    var collides = new FixedString128Bytes();
                    collides.Append((FixedString32Bytes)"collides 0x");
                    AppendHex8(ref collides, d.CollidesWithOverride);
                    Drawer.Text128(pos + new float3(0f, 0f, 0f), collides, TextColor, 10f);
                }
            }

            private static void AppendHex8(ref FixedString128Bytes s, uint value)
            {
                for (var shift = 28; shift >= 0; shift -= 4)
                {
                    var nibble = (int)((value >> shift) & 0xF);
                    s.Append((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
                }
            }
        }
    }
}
#endif