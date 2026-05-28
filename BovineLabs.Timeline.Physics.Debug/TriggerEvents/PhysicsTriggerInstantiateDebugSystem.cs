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
    public static class TriggerInstantiateDebugSystem
    {
        [ConfigVar("triggerinstgizmo.draw-enabled", true, "Enable the trigger instantiate gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("triggerinstgizmo.ghost-color", 0.1f, 0.8f, 0.2f, 0.6f, "Color for ghost transform (Greenish)")]
        public static readonly SharedStatic<Color> GhostColor = SharedStatic<Color>.GetOrCreate<Tags.GhostColor>();

        [ConfigVar("triggerinstgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct GhostColor { }
            public struct TextColor { }
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
                .WithAll<TrackBinding, PhysicsTriggerInstantiateData>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTriggerInstantiateGizmoSystem>(
                    ref state, TriggerInstantiateDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer     = drawer,
                GhostColor = TriggerInstantiateDebugSystem.GhostColor.Data,
                TextColor  = TriggerInstantiateDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public Color GhostColor;
            public Color TextColor;
            
            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;

            public void Execute(Entity entity, in TrackBinding binding, in PhysicsTriggerInstantiateData config)
            {
                var triggerEntity = binding.Value;
                if (!TransformLookup.TryGetComponent(triggerEntity, out var ltw))
                    return;

                var pos = ltw.Position;
                
                if (config.PositionMode == PhysicsTriggerPositionMode.MatchSelf)
                {
                    Drawer.Arrow(pos, new float3(1f, 0f, 0f), new Color(1f, 0f, 0f, GhostColor.a));
                    Drawer.Arrow(pos, new float3(0f, 1f, 0f), new Color(0f, 1f, 0f, GhostColor.a));
                    Drawer.Arrow(pos, new float3(0f, 0f, 1f), new Color(0f, 0f, 1f, GhostColor.a));
                }

                Drawer.Text32(pos + new float3(0f, 0.4f, 0f), $"Spawn: {config.ObjectId.ID}", TextColor, 10f);
                Drawer.Text32(pos + new float3(0f, 0.2f, 0f), $"on {config.EventState}", TextColor, 10f);
                
                if (config.PositionMode == PhysicsTriggerPositionMode.MatchCollidedEntity)
                    Drawer.Text32(pos + new float3(0f, -0.2f, 0f), "spawn @ other", TextColor, 10f);
                else if (config.PositionMode == PhysicsTriggerPositionMode.MatchContactPoint)
                    Drawer.Text32(pos + new float3(0f, -0.2f, 0f), "spawn @ contact", TextColor, 10f);
            }
        }
    }
}
#endif
