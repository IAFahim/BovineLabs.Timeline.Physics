using BovineLabs.Core;
using BovineLabs.Core.ConfigVars;
using BovineLabs.Quill;
using BovineLabs.Timeline.Core.Debug;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

#if UNITY_EDITOR || BL_DEBUG

namespace BovineLabs.Timeline.Physics.Debug
{
    [Configurable]
    public static class ExternalVelocityDebugSystem
    {
        [ConfigVar("externalvelocitygizmo.draw-enabled", false, "Draw the external (knockback) velocity channel.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        [ConfigVar("externalvelocitygizmo.color", 1.0f, 0.3f, 0.2f, 0.9f, "Color for the knockback channel arrow (Red)")]
        public static readonly SharedStatic<Color> ArrowColor = SharedStatic<Color>.GetOrCreate<Tags.ArrowColor>();

        private struct Tags
        {
            public struct Enabled
            {
            }

            public struct ArrowColor
            {
            }
        }
    }

    /// <summary>
    /// Per-body gizmo for the standing <see cref="ExternalVelocity"/> channel — an arrow in the knockback direction
    /// scaled by its speed, plus a close-up readout. Unlike the clip gizmos this is not track-bound; it draws any body
    /// currently carrying a non-zero external channel, so you can watch a hit land and decay while tuning the rate.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct PhysicsExternalVelocityGizmoSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();
            _query = SystemAPI.QueryBuilder()
                .WithAll<ExternalVelocity, LocalToWorld>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsExternalVelocityGizmoSystem>(
                    ref state, ExternalVelocityDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                Color = ExternalVelocityDebugSystem.ArrowColor.Data,
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;
            public Color Color;

            private void Execute(in ExternalVelocity external, in LocalToWorld ltw)
            {
                var speedSq = math.lengthsq(external.Linear);
                if (speedSq < 1e-4f)
                {
                    return;
                }

                var pos = ltw.Position;
                var speed = math.sqrt(speedSq);
                Drawer.Arrow(pos, math.normalize(external.Linear) * math.min(speed * 0.1f, 2f), Color);

                var tier = TimelineDebugTier.Resolve(pos, Viewer, HasViewer);
                if (tier >= DebugTier.Mid)
                {
                    Drawer.Text32(pos + new float3(0, 0.6f, 0), (FixedString32Bytes)"Knockback", Color, 10f);
                }

                if (tier == DebugTier.Close)
                {
                    var readout = new FixedString64Bytes();
                    readout.Append((FixedString32Bytes)"ext v ");
                    readout.Append(speed);
                    Drawer.Text64(pos + new float3(0, 0.4f, 0), readout, Color, 10f);
                }
            }
        }
    }
}
#endif
