#if UNITY_EDITOR || BL_DEBUG
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Core;
using BovineLabs.Timeline.Core.Debug;
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
    public static class StatefulEventDebugSystemConfig
    {
        [ConfigVar("statefuleventgizmo.draw-enabled", true, "Enable drawing of stateful trigger/collision events in Scene View.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("statefuleventgizmo.text-color", 0.1f, 0.9f, 0.1f, 0.9f, "Color for stateful event text labels")]
        public static readonly SharedStatic<Color> TextColor = SharedStatic<Color>.GetOrCreate<Tags.TextColor>();

        private struct Tags
        {
            public struct Enabled { }
            public struct TextColor { }
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

            public void Execute(Entity entity, in LocalToWorld ltw, in DynamicBuffer<StatefulTriggerEvent> triggers)
            {
                if (triggers.IsEmpty) return;

                var origin = ltw.Position + new float3(0f, 1f, 0f); // Offset slightly up
                
                FixedString128Bytes label = default;
                label.Append('T'); label.Append('r'); label.Append('i'); label.Append('g'); label.Append('g'); label.Append('e'); label.Append('r'); label.Append('s'); label.Append(':'); label.Append('\n');
                
                for (int i = 0; i < triggers.Length; i++)
                {
                    var evt = triggers[i];
                    
                    if (evt.State == StatefulEventState.Enter) { label.Append('E'); label.Append('N'); label.Append('T'); label.Append('E'); label.Append('R'); }
                    else if (evt.State == StatefulEventState.Stay) { label.Append('S'); label.Append('T'); label.Append('A'); label.Append('Y'); }
                    else if (evt.State == StatefulEventState.Exit) { label.Append('E'); label.Append('X'); label.Append('I'); label.Append('T'); }
                    
                    label.Append(' '); label.Append('(');
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

            public void Execute(Entity entity, in LocalToWorld ltw, in DynamicBuffer<StatefulCollisionEvent> collisions)
            {
                if (collisions.IsEmpty) return;

                var origin = ltw.Position + new float3(0f, 1f, 0f);
                
                FixedString128Bytes label = default;
                label.Append('C'); label.Append('o'); label.Append('l'); label.Append('l'); label.Append('i'); label.Append('s'); label.Append('i'); label.Append('o'); label.Append('n'); label.Append('s'); label.Append(':'); label.Append('\n');
                
                for (int i = 0; i < collisions.Length; i++)
                {
                    var evt = collisions[i];
                    
                    if (evt.State == StatefulEventState.Enter) { label.Append('E'); label.Append('N'); label.Append('T'); label.Append('E'); label.Append('R'); }
                    else if (evt.State == StatefulEventState.Stay) { label.Append('S'); label.Append('T'); label.Append('A'); label.Append('Y'); }
                    else if (evt.State == StatefulEventState.Exit) { label.Append('E'); label.Append('X'); label.Append('I'); label.Append('T'); }
                    
                    label.Append(' '); label.Append('(');
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
