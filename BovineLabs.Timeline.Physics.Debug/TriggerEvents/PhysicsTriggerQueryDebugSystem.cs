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
    public static class TriggerQueryDebugSystem
    {
        [ConfigVar("triggerquerygizmo.draw-enabled", false, "Enable the trigger query selection gizmo.")]
        public static readonly SharedStatic<bool> Enabled = SharedStatic<bool>.GetOrCreate<Tags.Enabled>();

        private struct Tags
        {
            public struct Enabled
            {
            }
        }
    }

    /// <summary>
    /// Visualises a <see cref="PhysicsTriggerQueryData"/> filter on a StatefulTriggerTrack: the acquisition origin,
    /// the maxDistance / maxAngle gates, the trigger candidates coloured by role (winner / survivor / rejected), the
    /// DirectionSector spokes with the active wedge lit, the bearing toward the winner and the −bearing knock
    /// direction. The sector classification reuses <see cref="PhysicsTriggerSectorMath"/> so the gizmo matches the
    /// runtime value exactly. Matches the clip whether or not it is active (<see cref="ClipActive"/> is read, not
    /// required): a configured-but-inactive query draws FADED, an active (firing) one draws at full strength — so a
    /// designer can see the filter exists without mistaking it for always-on. The live parts (candidates, winner,
    /// bearing, active wedge) only appear while the clip is actually active.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation |
                       WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(DebugSystemGroup))]
    public partial struct PhysicsTriggerQueryDebugSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DrawSystem.Singleton>();

            // NOTE: deliberately NOT gated on ClipActive. ClipActive only exists in the PLAY world (the timeline is
            // running), but the editor world's DrawSystem is the one that draws unconditionally (the IsEditorWorld
            // path) — play-mode game-view drawing is gated behind the internal GlobalDraw flag. Matching the baked
            // query clip entity without ClipActive lets the static filter map (origin + maxDistance ring + maxAngle
            // cone + DirectionSector spokes + label) render in the SCENE VIEW at EDIT time, with no timeline running.
            // The live parts (candidates from the trigger buffer, the winner/bearing/active-wedge) simply no-op at
            // edit time (empty buffer, LastWinner == Null) and light up during play.
            _query = SystemAPI.QueryBuilder()
                .WithAll<TrackBinding, PhysicsTriggerQueryData, PhysicsTriggerQueryState>()
                .WithPresent<ClipActive>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!TimelineDebugUtility.TryGetDrawer<PhysicsTriggerQueryDebugSystem>(
                    ref state, TriggerQueryDebugSystem.Enabled.Data, out var drawer,
                    out var viewer, out var hasViewer))
                return;

            state.Dependency = new DrawJob
            {
                Drawer = drawer,
                Viewer = viewer,
                HasViewer = hasViewer,
                TransformLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true),
                ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
                TriggerEventsLookup = SystemAPI.GetBufferLookup<StatefulTriggerEvent>(true)
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        [WithPresent(typeof(ClipActive))]
        private partial struct DrawJob : IJobEntity
        {
            public Drawer Drawer;
            public float3 Viewer;
            public bool HasViewer;

            // Inactive (clip not currently active) draws FADED so designers can see a query is configured but not
            // firing; an active clip draws at full strength. Prevents the "it's always on" misread.
            private const float InactiveAlpha = 0.28f;

            [ReadOnly] public ComponentLookup<LocalToWorld> TransformLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;
            [ReadOnly] public BufferLookup<StatefulTriggerEvent> TriggerEventsLookup;

            private const int RingSegments = 24;

            // Survivor / rejected colours (reuse PID green / Error red; winner uses CustomLink green-bold marker).
            private static readonly Color WinnerColor = new(0.1f, 1.0f, 0.2f, 0.95f);
            private static readonly Color SurvivorColor = new(0.9f, 0.85f, 0.1f, 0.85f); // yellow
            private static readonly Color RejectedColor = new(0.8f, 0.15f, 0.1f, 0.4f); // dim red

            private void Execute(in TrackBinding binding, in PhysicsTriggerQueryData config,
                in PhysicsTriggerQueryState queryState, EnabledRefRO<ClipActive> clipActiveState)
            {
                var self = binding.Value;
                if (!TryResolveSelf(self, out var selfPos, out var selfRot))
                    return;

                // Is the clip currently active (firing) or merely configured? Drives the faded/bold treatment.
                var active = clipActiveState.ValueRO;

                var tier = TimelineDebugTier.Resolve(selfPos, Viewer, HasViewer);

                var forward = math.rotate(selfRot, math.forward());
                if (!math.all(math.isfinite(forward)))
                    return;

                // The sector basis EXACTLY as the runtime resolves it (fwd = World +Z or self forward; up by plane).
                var sectorFwd = config.SectorReference == PhysicsTriggerSectorReference.World
                    ? new float3(0f, 0f, 1f)
                    : forward;
                var up = ResolveSectorUp(in config, selfRot);

                // 1. Origin (slightly smaller + faded when the clip isn't active).
                Drawer.Point(selfPos, active ? 0.12f : 0.09f, Fade(TimelineDebugColors.Anchor, active));

                // 2. maxDistance gate — a ring in the sector plane.
                if (config.MaxDistance > 0f)
                    DrawRing(selfPos, up, config.MaxDistance, Fade(TimelineDebugColors.Connection, active));

                // 3. maxAngle cone — forward + two edge rays at ±MaxAngle around forward in the sector plane.
                if (config.MaxAngle > 0f)
                    DrawViewCone(selfPos, sectorFwd, up, config.MaxAngle,
                        config.MaxDistance > 0f ? config.MaxDistance : 4f, active);

                // 5. DirectionSector spokes + active wedge (drawn under the candidate lines so winners read on top).
                var winner = queryState.LastWinner;
                var isSector = config.ValueMode == PhysicsTriggerQueryValueMode.DirectionSector;
                if (isSector && config.SectorCount >= 1)
                    DrawSectorSpokes(selfPos, sectorFwd, up, in config, winner, active);

                // 4. Candidates — iterate the bound entity's trigger buffer; colour by role.
                DrawCandidates(self, selfPos, forward, in config, in queryState, tier);

                // 5/6. Bearing toward the winner + the −bearing knock arrow.
                if (winner != Entity.Null && TryResolvePos(winner, out var winnerPos))
                {
                    var off = winnerPos - selfPos;
                    if (math.lengthsq(off) > PhysicsTriggerSectorMath.Epsilon)
                    {
                        var bearing = math.normalize(off);
                        // Bold bearing arrow from self toward the winner.
                        Drawer.Arrow(selfPos, bearing * math.length(off), WinnerColor);
                        // 6. Away / knock direction — where the force would push.
                        Drawer.Arrow(selfPos, -bearing * 1.5f, TimelineDebugColors.LinearForce);
                    }
                }

                // 5. Live sector label (graceful sentinel handling).
                if (isSector && tier >= DebugTier.Mid)
                    DrawSectorLabel(selfPos, in config, in queryState, winner, sectorFwd, up, active);
            }

            // Fade a colour's alpha when the clip is inactive (configured-but-not-firing).
            private static Color Fade(Color c, bool active)
            {
                return active ? c : new Color(c.r, c.g, c.b, c.a * InactiveAlpha);
            }

            // -----------------------------------------------------------------------------------------------
            // Candidates
            // -----------------------------------------------------------------------------------------------

            private void DrawCandidates(Entity self, float3 selfPos, float3 forward,
                in PhysicsTriggerQueryData config, in PhysicsTriggerQueryState queryState, DebugTier tier)
            {
                if (!TriggerEventsLookup.TryGetBuffer(self, out var triggers))
                    return;

                var maxDistSq = config.MaxDistance > 0f ? config.MaxDistance * config.MaxDistance : float.MaxValue;
                var minAlignment = config.MaxAngle > 0f ? math.cos(config.MaxAngle) : float.MinValue;

                foreach (var evt in triggers)
                {
                    var other = evt.EntityB;
                    if (other == Entity.Null || !TryResolvePos(other, out var otherPos))
                        continue;

                    var off = otherPos - selfPos;
                    var distSq = math.lengthsq(off);
                    var alignment = distSq > 1e-8f ? math.dot(forward, off * math.rsqrt(distSq)) : 1f;

                    Color color;
                    var bold = false;
                    if (other == queryState.LastWinner)
                    {
                        color = WinnerColor;
                        bold = true;
                    }
                    else if (distSq > maxDistSq || alignment < minAlignment)
                    {
                        // Cheap reject test (distance / cone) — RRD/dim. Not a full re-run of every gate.
                        color = RejectedColor;
                    }
                    else
                    {
                        color = SurvivorColor;
                    }

                    Drawer.Line(selfPos, otherPos, color);
                    Drawer.Point(otherPos, bold ? 0.25f : 0.12f, color);
                    if (bold)
                        Drawer.Sphere(otherPos, 0.3f, 12, WinnerColor);
                }
            }

            // -----------------------------------------------------------------------------------------------
            // Gate geometry
            // -----------------------------------------------------------------------------------------------

            private void DrawRing(float3 center, float3 up, float radius, Color color)
            {
                // Line-loop of RingSegments in the plane perpendicular to up.
                var n = math.normalizesafe(up, new float3(0f, 1f, 0f));
                var basisA = math.normalizesafe(math.cross(n, new float3(0f, 0f, 1f)), new float3(1f, 0f, 0f));
                if (math.lengthsq(math.cross(n, new float3(0f, 0f, 1f))) < 1e-6f)
                    basisA = math.normalizesafe(math.cross(n, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
                var basisB = math.cross(n, basisA);

                var prev = center + basisA * radius;
                for (var i = 1; i <= RingSegments; i++)
                {
                    var a = i / (float)RingSegments * 2f * math.PI;
                    var p = center + (basisA * math.cos(a) + basisB * math.sin(a)) * radius;
                    Drawer.Line(prev, p, color);
                    prev = p;
                }
            }

            private void DrawViewCone(float3 origin, float3 fwd, float3 up, float halfAngle, float length, bool active)
            {
                var n = math.normalizesafe(up, new float3(0f, 1f, 0f));
                var fwdN = math.normalizesafe(fwd - n * math.dot(fwd, n), new float3(0f, 0f, 1f));
                var right = math.cross(n, fwdN);

                var fwdRay = fwdN * length;
                Drawer.Line(origin, origin + fwdRay, Fade(TimelineDebugColors.Connection, active));

                var leftDir = fwdN * math.cos(halfAngle) - right * math.sin(halfAngle);
                var rightDir = fwdN * math.cos(halfAngle) + right * math.sin(halfAngle);
                Drawer.Arrow(origin, leftDir * length, Fade(TimelineDebugColors.OwnerLink, active));
                Drawer.Arrow(origin, rightDir * length, Fade(TimelineDebugColors.OwnerLink, active));
            }

            // -----------------------------------------------------------------------------------------------
            // DirectionSector visualisation
            // -----------------------------------------------------------------------------------------------

            private void DrawSectorSpokes(float3 origin, float3 fwd, float3 up, in PhysicsTriggerQueryData config,
                Entity winner, bool active)
            {
                var sectorCount = config.SectorCount;
                var n = math.normalizesafe(up, new float3(0f, 1f, 0f));
                var fwdN = math.normalizesafe(fwd - n * math.dot(fwd, n), new float3(0f, 0f, 1f));
                var right = math.cross(n, fwdN); // self-right in LH

                var radius = config.MaxDistance > 0f ? config.MaxDistance : 4f;
                var binW = 2f * math.PI / sectorCount;

                // The active sector for the current winner (sentinel == sectorCount → no active wedge).
                var activeSector = sectorCount; // sentinel default
                if (winner != Entity.Null && TryResolvePos(winner, out var winnerPos))
                    activeSector = PhysicsTriggerSectorMath.ComputeSector(winnerPos - origin, fwd, up, sectorCount);

                // Spokes radiate along the BOUNDARIES between bins; bin k is centred on angle k*binW (bin 0 = fwd).
                for (var k = 0; k < sectorCount; k++)
                {
                    var boundary = (k + 0.5f) * binW; // edge between bin k and k+1
                    var dir = fwdN * math.cos(boundary) + right * math.sin(boundary);
                    Drawer.Line(origin, origin + dir * radius, Fade(TimelineDebugColors.Connection, active));
                }

                // Light up the active wedge (fan of short segments across the active bin).
                if (activeSector >= 0 && activeSector < sectorCount)
                {
                    var start = (activeSector - 0.5f) * binW;
                    const int fan = 6;
                    var prev = origin;
                    var first = true;
                    for (var s = 0; s <= fan; s++)
                    {
                        var a = start + s / (float)fan * binW;
                        var dir = fwdN * math.cos(a) + right * math.sin(a);
                        var p = origin + dir * radius;
                        if (!first)
                            Drawer.Line(prev, p, WinnerColor);
                        Drawer.Line(origin, p, TimelineDebugColors.PidGoal);
                        prev = p;
                        first = false;
                    }
                }
            }

            private void DrawSectorLabel(float3 origin, in PhysicsTriggerQueryData config,
                in PhysicsTriggerQueryState queryState, Entity winner, float3 fwd, float3 up, bool active)
            {
                // The LIVE classified sector for the winner (matches runtime ComputeSector path).
                var sector = config.SectorCount; // sentinel
                if (winner != Entity.Null && TryResolvePos(winner, out var winnerPos))
                    sector = PhysicsTriggerSectorMath.ComputeSector(winnerPos - origin, fwd, up, config.SectorCount);

                var label = new FixedString128Bytes();
                label.Append((FixedString32Bytes)"sector ");
                if (sector < 0 || sector >= config.SectorCount)
                    label.Append('-'); // undefined / degenerate sentinel
                else
                    label.Append(sector);

                Drawer.Text128(origin + new float3(0f, 0.5f, 0f), label, Fade(TimelineDebugColors.Label, active), 11f);
            }

            // -----------------------------------------------------------------------------------------------
            // Shared resolution (mirrors the runtime / sibling debug systems)
            // -----------------------------------------------------------------------------------------------

            private static float3 ResolveSectorUp(in PhysicsTriggerQueryData config, quaternion selfRot)
            {
                switch (config.SectorPlane)
                {
                    case PhysicsTriggerSectorPlane.ViewRelative:
                        return math.rotate(selfRot, new float3(0f, 1f, 0f));
                    case PhysicsTriggerSectorPlane.CustomAxis:
                        return config.SectorCustomUp;
                    default:
                        return new float3(0f, 1f, 0f);
                }
            }

            private bool TryResolveSelf(Entity self, out float3 pos, out quaternion rot)
            {
                pos = float3.zero;
                rot = quaternion.identity;
                if (self == Entity.Null)
                    return false;

                // Anti-jitter: prefer an unparented LocalTransform (the convention in the sibling trigger systems).
                if (LocalTransformLookup.HasComponent(self) && !ParentLookup.HasComponent(self))
                {
                    var lt = LocalTransformLookup[self];
                    pos = lt.Position;
                    rot = lt.Rotation;
                    return true;
                }

                if (TransformLookup.TryGetComponent(self, out var ltw))
                {
                    pos = ltw.Position;
                    rot = new quaternion(math.orthonormalize(new float3x3(ltw.Value)));
                    return true;
                }

                return false;
            }

            private bool TryResolvePos(Entity e, out float3 pos)
            {
                if (LocalTransformLookup.HasComponent(e) && !ParentLookup.HasComponent(e))
                {
                    pos = LocalTransformLookup[e].Position;
                    return true;
                }

                if (TransformLookup.TryGetComponent(e, out var ltw))
                {
                    pos = ltw.Position;
                    return true;
                }

                pos = float3.zero;
                return false;
            }
        }
    }
}
#endif
