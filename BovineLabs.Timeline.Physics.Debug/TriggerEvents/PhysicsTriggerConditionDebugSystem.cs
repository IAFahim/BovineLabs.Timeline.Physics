#if UNITY_EDITOR || BL_DEBUG

using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
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
    public static class TriggerConditionDebugSystem
    {
        [ConfigVar("triggergizmo.draw-enabled", false, "Enable the trigger condition gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("triggergizmo.route-color", 0.8f, 0.8f, 0.1f, 0.8f, "Color for route wiring (Yellow)")]
        public static readonly SharedStatic<Color> RouteColor = SharedStatic<Color>.GetOrCreate<Tags.RouteColor>();

        [ConfigVar("triggergizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct RouteColor
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
    public partial struct PhysicsTriggerConditionGizmoSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerConditionData, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTriggerConditionGizmoSystem>(
                    ref state, TriggerConditionDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                RouteColor = TriggerConditionDebugSystem.RouteColor.Data,
                TextColor = TriggerConditionDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                TargetsLookup = state.GetUnsafeComponentLookup<Targets>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public Color RouteColor;
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

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in PhysicsTriggerConditionData config)
            {
                var isFirstFrame =
                    false;

                var triggerEntity = binding.Value;
                if (!TransformLookup.TryGetComponent(triggerEntity, out var triggerLtw))
                    return;

                var pos = triggerLtw.Position;
                var tier = TimelineDebugTier.Resolve(pos, Viewer, HasViewer);

                // Far: what the system does — the enter/stay/exit arrow marker.
                var markerSize = 0.5f;
                if (config.EventState == StatefulEventState.Enter)
                {
                    Drawer.Line(pos + new float3(-markerSize, 0, markerSize), pos, RouteColor);
                    Drawer.Line(pos + new float3(markerSize, 0, markerSize), pos, RouteColor);
                }
                else if (config.EventState == StatefulEventState.Exit)
                {
                    Drawer.Line(pos, pos + new float3(-markerSize, 0, markerSize), RouteColor);
                    Drawer.Line(pos, pos + new float3(markerSize, 0, markerSize), RouteColor);
                }
                else
                {
                    Drawer.Line(pos + new float3(-markerSize, 0, 0), pos + new float3(markerSize, 0, 0), RouteColor);
                }

                if (tier >= DebugTier.Mid)
                {
                    Drawer.Text32(pos + new float3(0f, 0.5f, 0f), (FixedString32Bytes)"Trigger Cond", TextColor, 10f);
                    var drawColor = new Color(1f, 0.4f, 0f, 0.8f);
                    TriggerGizmoUtility.DrawActuallyFired(
                        triggerEntity, config.EventState, pos, ref Drawer,
                        TriggerEventsLookup, CollisionEventsLookup, drawColor, "TRIGGERED!");
                }

                if (tier == DebugTier.Close)
                {
                    var readout = new FixedString128Bytes();
                    readout.Append((FixedString32Bytes)"cond ");
                    readout.Append((int)(ushort)config.Condition);
                    readout.Append((FixedString32Bytes)"  on ");
                    if (config.EventState == StatefulEventState.Enter)
                        readout.Append((FixedString32Bytes)"Enter");
                    else if (config.EventState == StatefulEventState.Exit)
                        readout.Append((FixedString32Bytes)"Exit");
                    else
                        readout.Append((FixedString32Bytes)"Stay");
                    Drawer.Text128(pos + new float3(0f, 0.75f, 0f), readout, TextColor, 10f);
                }
            }
        }
    }
}
#endif