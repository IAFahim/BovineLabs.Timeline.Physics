#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class StatefulEventDebugSystemConfig
    {
        [ConfigVar("statefuleventgizmo.draw-enabled", false,
            "Enable drawing of stateful trigger/collision events in Scene View.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("statefuleventgizmo.text-color", 0.1f, 0.9f, 0.1f, 0.9f, "Color for stateful event text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled
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
    public partial struct PhysicsStatefulEventDebugSystem : ISystem
    {
        private EntityQuery _triggerQuery;
        private EntityQuery _collisionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _triggerQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalToWorld, StatefulTriggerEvent>()
                .Build();

            _collisionQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalToWorld, StatefulCollisionEvent>()
                .Build();

            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAny<StatefulTriggerEvent, StatefulCollisionEvent>()
                .Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsStatefulEventDebugSystem>(
                    ref state, StatefulEventDebugSystemConfig.Enabled.Data, out var drawer))
                return;

            state.Dependency = new DrawTriggerJob
            {
                Drawer = drawer,
                TextColor = StatefulEventDebugSystemConfig.TextColor.Data
            }.ScheduleParallel(_triggerQuery, state.Dependency);

            state.Dependency = new DrawCollisionJob
            {
                Drawer = drawer,
                TextColor = StatefulEventDebugSystemConfig.TextColor.Data
            }.ScheduleParallel(_collisionQuery, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawTriggerJob : IJobEntity
        {
            public Drawer Drawer;
            public Color TextColor;

            private static readonly FixedString32Bytes Title = "Triggers:\n";
            private static readonly FixedString32Bytes Enter = "ENTER";
            private static readonly FixedString32Bytes Stay = "STAY";
            private static readonly FixedString32Bytes Exit = "EXIT";

            public void Execute(Entity entity, in LocalToWorld ltw, in DynamicBuffer<StatefulTriggerEvent> triggers)
            {
                if (triggers.IsEmpty) return;

                var origin = ltw.Position + new float3(0f, 1f, 0f);

                FixedString128Bytes label = default;
                label.Append(Title);

                for (var i = 0; i < triggers.Length; i++)
                {
                    var evt = triggers[i];

                    if (evt.State == StatefulEventState.Enter)
                        label.Append(Enter);
                    else if (evt.State == StatefulEventState.Stay)
                        label.Append(Stay);
                    else if (evt.State == StatefulEventState.Exit) label.Append(Exit);

                    label.Append(' ');
                    label.Append('(');
                    label.Append(evt.EntityB.Index);
                    label.Append(':');
                    label.Append(evt.EntityB.Version);
                    label.Append(')');
                    label.Append('\n');
                }

                Drawer.Text128(origin, label, TextColor, 10f);
            }
        }

        [BurstCompile]
        private partial struct DrawCollisionJob : IJobEntity
        {
            public Drawer Drawer;
            public Color TextColor;

            private static readonly FixedString32Bytes Title = "Collisions:\n";
            private static readonly FixedString32Bytes Enter = "ENTER";
            private static readonly FixedString32Bytes Stay = "STAY";
            private static readonly FixedString32Bytes Exit = "EXIT";

            public void Execute(Entity entity, in LocalToWorld ltw, in DynamicBuffer<StatefulCollisionEvent> collisions)
            {
                if (collisions.IsEmpty) return;

                var origin = ltw.Position + new float3(0f, 1f, 0f);

                FixedString128Bytes label = default;
                label.Append(Title);

                for (var i = 0; i < collisions.Length; i++)
                {
                    var evt = collisions[i];

                    if (evt.State == StatefulEventState.Enter)
                        label.Append(Enter);
                    else if (evt.State == StatefulEventState.Stay)
                        label.Append(Stay);
                    else if (evt.State == StatefulEventState.Exit) label.Append(Exit);

                    label.Append(' ');
                    label.Append('(');
                    label.Append(evt.EntityB.Index);
                    label.Append(':');
                    label.Append(evt.EntityB.Version);
                    label.Append(')');
                    label.Append('\n');
                }

                Drawer.Text128(origin, label, TextColor, 10f);
            }
        }
    }
}
#endif