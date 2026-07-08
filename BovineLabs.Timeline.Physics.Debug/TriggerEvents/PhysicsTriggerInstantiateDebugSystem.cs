#if UNITY_EDITOR || BL_DEBUG

using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Nerve.ObjectManagement;
using BovineLabs.Nerve.PhysicsStates;
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
    public static class TriggerInstantiateDebugSystem
    {
        [ConfigVar("triggerinstgizmo.draw-enabled", false, "Enable the trigger instantiate gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("triggerinstgizmo.ghost-color", 0.1f, 0.8f, 0.2f, 0.6f, "Color for ghost transform (Greenish)")]
        public static readonly SharedStatic<Color> GhostColor = SharedStatic<Color>.GetOrCreate<Tags.GhostColor>();

        [ConfigVar("triggerinstgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct GhostColor
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
    public partial struct PhysicsTriggerInstantiateGizmoSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerInstantiateData, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTriggerInstantiateGizmoSystem>(
                    ref state, TriggerInstantiateDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            var names = new NativeHashMap<ObjectId, FixedString64Bytes>(16, Allocator.TempJob);
            if (SystemAPI.TryGetSingleton<ObjectDefinitionRegistry>(out var registry))
                foreach (var config in SystemAPI.Query<RefRO<PhysicsTriggerInstantiateData>>()
                             .WithAll<TrackBinding, ClipActive>())
                {
                    var id = config.ValueRO.ObjectId;
                    if (names.ContainsKey(id))
                        continue;

                    if (registry.TryGetValue(id, out var prefab) && state.EntityManager.Exists(prefab))
                    {
                        state.EntityManager.GetName(prefab, out var name);
                        names.Add(id, name);
                    }
                }

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                GhostColor = TriggerInstantiateDebugSystem.GhostColor.Data,
                TextColor = TriggerInstantiateDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true),
                Names = names
            }.ScheduleParallel(_query, state.Dependency);

            names.Dispose(state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public Color GhostColor;
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

            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;
            [ReadOnly] public BufferLookup<StatefulCollisionEvent> CollisionEventsLookup;
            [ReadOnly] public NativeHashMap<ObjectId, FixedString64Bytes> Names;

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in PhysicsTriggerInstantiateData config)
            {
                var triggerEntity = binding.Value;
                if (!TransformLookup.TryGetComponent(triggerEntity, out var ltw))
                    return;

                var pos = ltw.Position;
                var tier = TimelineDebugTier.Resolve(pos, Viewer, HasViewer);

                Drawer.Sphere(pos, 0.1f, 8, GhostColor);

                if (tier == DebugTier.Mid)
                    Drawer.Text32(pos + new float3(0f, 0.3f, 0f), (FixedString32Bytes)"Spawn", TextColor, 10f);

                if (tier == DebugTier.Close)
                {
                    var spawn = Names.TryGetValue(config.ObjectId, out var nm)
                        ? nm
                        : config.ObjectId.ToFixedString();
                    var placement = config.PositionMode == PhysicsTriggerPositionMode.MatchCollidedEntity
                        ? (FixedString32Bytes)" @ other"
                        : config.PositionMode == PhysicsTriggerPositionMode.MatchContactPoint
                            ? (FixedString32Bytes)" @ contact"
                            : default;

                    FixedString128Bytes text = default;
                    text.Append(spawn);
                    text.Append((FixedString64Bytes)$"\non {config.EventState}{placement}");
                    Drawer.Text128(pos + new float3(0f, 0.3f, 0f), text, TextColor, 10f);
                }

                if (tier >= DebugTier.Mid)
                {
                    var drawColor = new Color(0f, 1f, 0f, 0.8f);
                    TriggerGizmoUtility.DrawActuallyFired(
                        triggerEntity, config.EventState, pos, ref Drawer,
                        TriggerEventsLookup, CollisionEventsLookup, drawColor, "spawned", 0.25f, 11f);
                }
            }
        }
    }
}
#endif