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
            public struct Enabled { }
            public struct RouteColor { }
            public struct TextColor { }
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
                  ref state, TriggerConditionDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer     = drawer,
                RouteColor = TriggerConditionDebugSystem.RouteColor.Data,
                TextColor  = TriggerConditionDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                TargetsLookup   = state.GetUnsafeComponentLookup<Targets>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public Color RouteColor;
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

            [ReadOnly] public UnsafeComponentLookup<Targets> TargetsLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in TrackBinding binding, in PhysicsTriggerConditionData config)
            {
                // We're omitting the accurate isFirstFrame check for the debug visualizer for simplicity since it's just visual.
                // Or we can query ClipActivePrevious. Let's just pass `true` to isFirstFrame so it draws Enter/Stay.
                bool isFirstFrame = false; // By default false, StatefulEventMatching will still work for Stay/Exit. Enter requires isFirstFrame.
                
                var triggerEntity = binding.Value;
                if (!TransformLookup.TryGetComponent(triggerEntity, out var triggerLtw))
                    return;

                var pos = triggerLtw.Position;
                
                Drawer.Text32(pos + new float3(0f, 0.5f, 0f), $"-> {config.Condition}", TextColor, 10f);

                // Draw chevron/marker for State
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
                
                // --- Actually Fired Visualizer ---
                var drawColor = new Color(1f, 0.4f, 0f, 0.8f);
                TriggerGizmoUtility.DrawActuallyFired(
                    triggerEntity, config.EventState, pos, ref Drawer,
                    TriggerEventsLookup, CollisionEventsLookup, drawColor, "TRIGGERED!");
            }
        }
    }
}
#endif
