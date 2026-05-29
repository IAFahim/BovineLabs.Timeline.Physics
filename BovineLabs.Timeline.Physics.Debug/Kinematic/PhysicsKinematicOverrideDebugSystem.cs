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
    public static class KinematicOverrideDebugSystem
    {
        [ConfigVar("kinematicgizmo.draw-enabled", false, "Enable the kinematic override gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("kinematicgizmo.box-color", 0.5f, 0.5f, 0.5f, 0.8f, "Color for kinematic wireframe box")]
        public static readonly SharedStatic<Color> BoxColor = SharedStatic<Color>.GetOrCreate<Tags.BoxColor>();

        [ConfigVar("kinematicgizmo.zero-g-color", 0.8f, 0.8f, 0.8f, 0.9f, "Color for zero-G marker")]
        public static readonly SharedStatic<Color> ZeroGColor = SharedStatic<Color>.GetOrCreate<Tags.ZeroGColor>();

        [ConfigVar("kinematicgizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct BoxColor { }
            public struct ZeroGColor { }
            public struct TextColor { }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsKinematicOverrideGizmoSystem : ISystem
    {
        private EntityQuery _query;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsKinematicOverrideAnimated, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsKinematicOverrideGizmoSystem>(
                  ref state, KinematicOverrideDebugSystem.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer     = drawer,
                BoxColor   = KinematicOverrideDebugSystem.BoxColor.Data,
                ZeroGColor = KinematicOverrideDebugSystem.ZeroGColor.Data,
                TextColor  = KinematicOverrideDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public Color BoxColor;
            public Color ZeroGColor;
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


            public void Execute(Entity entity, in TrackBinding binding, in PhysicsKinematicOverrideAnimated animated)
            {
                var target = binding.Value;
                if (!TransformLookup.TryGetComponent(target, out var ltw))
                    return;

                var d = animated.Value;
                var pos = GetAntiJitterPosition(target, ltw.Position);
                var rot = ltw.Rotation;

                if (d.IsKinematic)
                {
                    Drawer.Circle(pos, new float3(0f, 0.5f, 0f), BoxColor);
                    Drawer.Circle(pos, math.mul(rot, new float3(0.5f, 0f, 0f)), BoxColor);
                    Drawer.Circle(pos, math.mul(rot, new float3(0f, 0f, 0.5f)), BoxColor);
                    Drawer.Text32(pos + new float3(0f, 0.8f, 0f), "KINEMATIC", TextColor, 10f);
                }

                if (d.ZeroGravity)
                {
                    Drawer.Text32(pos + new float3(0f, 0.5f, 0f), "g×0", ZeroGColor, 12f);
                    Drawer.Circle(pos + new float3(0f, 0.5f, 0f), new float3(0f, 0.15f, 0f), ZeroGColor);
                }
            }
        }
    }
}
#endif
