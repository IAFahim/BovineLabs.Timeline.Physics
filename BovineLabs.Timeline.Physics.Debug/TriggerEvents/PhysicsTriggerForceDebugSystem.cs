#if UNITY_EDITOR || BL_DEBUG

using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.PhysicsStates;
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
    public static class TriggerForceDebugSystem
    {
        [ConfigVar("triggerforcegizmo.draw-enabled", false, "Enable the trigger force gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("triggerforcegizmo.force-color", 0.8f, 0.8f, 0.1f, 0.8f, "Color for force lines (Yellow)")]
        public static readonly SharedStatic<Color> ForceColor = SharedStatic<Color>.GetOrCreate<Tags.ForceColor>();

        [ConfigVar("triggerforcegizmo.text-color", 1.0f, 1.0f, 1.0f, 0.9f, "Color for text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct ForceColor
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
    public partial struct PhysicsTriggerForceGizmoSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerForceData, ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTriggerForceGizmoSystem>(
                    ref state, TriggerForceDebugSystem.Enabled.Data, out var drawer))
                return;
            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                ForceColor = TriggerForceDebugSystem.ForceColor.Data,
                TextColor = TriggerForceDebugSystem.TextColor.Data,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true),
                CollisionEventsLookup = SystemAPI.GetBufferLookup<StatefulCollisionEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public Color ForceColor;
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

            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in TrackBinding binding,
                in PhysicsTriggerForceData config)
            {
                var triggerEntity = binding.Value;
                if (!TransformLookup.TryGetComponent(triggerEntity, out var ltw))
                    return;

                var pos = ltw.Position;
                var origin = pos;

                Drawer.Sphere(origin, 0.1f, 8, ForceColor);

                var label = new FixedString32Bytes();
                if (config.ForceType == PhysicsTriggerForceType.Directional)
                {
                    label.Append('D');
                    label.Append('i');
                    label.Append('r');
                    label.Append(' ');
                }
                else if (config.ForceType == PhysicsTriggerForceType.Radial)
                {
                    label.Append('R');
                    label.Append('a');
                    label.Append('d');
                    label.Append(' ');
                }
                else
                {
                    label.Append('V');
                    label.Append('o');
                    label.Append('r');
                    label.Append(' ');
                }

                label.Append(config.Magnitude);
                label.Append('\n');

                if (config.FalloffCurve == PhysicsTriggerFalloffCurve.None)
                {
                    label.Append('N');
                    label.Append('o');
                    label.Append('n');
                    label.Append('e');
                }
                else if (config.FalloffCurve == PhysicsTriggerFalloffCurve.Linear)
                {
                    label.Append('L');
                    label.Append('i');
                    label.Append('n');
                }
                else if (config.FalloffCurve == PhysicsTriggerFalloffCurve.InverseSquare)
                {
                    label.Append('I');
                    label.Append('n');
                    label.Append('v');
                }
                else
                {
                    label.Append('S');
                    label.Append('t');
                    label.Append('e');
                    label.Append('p');
                }

                label.Append('\n');
                if (config.Mode == PhysicsForceMode.Impulse)
                {
                    label.Append('[');
                    label.Append('I');
                    label.Append('M');
                    label.Append('P');
                    label.Append(']');
                }
                else
                {
                    label.Append('[');
                    label.Append('C');
                    label.Append('O');
                    label.Append('N');
                    label.Append('T');
                    label.Append(']');
                }

                Drawer.Text32(origin + new float3(0f, 0.5f, 0f), label, TextColor, 10f);

                if (config.FalloffCurve != PhysicsTriggerFalloffCurve.None)
                {
                    Drawer.Circle(origin, math.rotate(ltw.Rotation, new float3(0f, config.FalloffStartRadius, 0f)),
                        ForceColor);
                    var outerColor = ForceColor;
                    outerColor.a *= 0.3f;
                    Drawer.Circle(origin, math.rotate(ltw.Rotation, new float3(0f, config.FalloffEndRadius, 0f)),
                        outerColor);
                }

                if (config.ForceType == PhysicsTriggerForceType.Directional)
                {
                    var localDir = math.normalizesafe(config.Direction, new float3(0, 0, 1));
                    var globalDir = math.rotate(ltw.Rotation, localDir);
                    if (math.lengthsq(globalDir) > 0.01f)
                    {
                        var rot = quaternion.LookRotationSafe(globalDir, math.up());
                        Drawer.Arrow(origin, globalDir * 2f, ForceColor);
                    }
                }
                else if (config.ForceType == PhysicsTriggerForceType.Radial)
                {
                    var rays = 8;
                    for (var i = 0; i < rays; i++)
                    {
                        var angle = i / (float)rays * math.PI * 2f;
                        var dir = new float3(math.cos(angle), 0, math.sin(angle));
                        if (config.Magnitude < 0) dir = -dir;
                        var globalDir = math.rotate(ltw.Rotation, dir);
                        Drawer.Arrow(origin + globalDir * 0.5f, globalDir * 1.5f, ForceColor);
                    }
                }
                else if (config.ForceType == PhysicsTriggerForceType.Vortex)
                {
                    var rays = 4;
                    for (var i = 0; i < rays; i++)
                    {
                        var angle = i / (float)rays * math.PI * 2f;
                        var posOffset = new float3(math.cos(angle), 0, math.sin(angle)) * 1f;
                        var tangent = new float3(-posOffset.z, 0, posOffset.x);
                        if (config.Magnitude < 0) tangent = -tangent;
                        tangent = math.normalize(tangent);

                        var globalPosOffset = math.rotate(ltw.Rotation, posOffset);
                        var globalTangent = math.rotate(ltw.Rotation, tangent);
                        Drawer.Arrow(origin + globalPosOffset, globalTangent * 1.5f, ForceColor);
                    }
                }

                // --- Actually Fired Visualizer ---
                var drawColor = new Color(0f, 0.8f, 1f, 0.8f);
                TriggerGizmoUtility.DrawActuallyFired(
                    triggerEntity, config.EventState, pos, ref Drawer,
                    TriggerEventsLookup, CollisionEventsLookup, drawColor, "FORCE APPLIED");
            }
        }
    }
}
#endif