#if UNITY_EDITOR
using System.Collections.Generic;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Authoring;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Editor
{
    /// <summary>
    ///     Approximate, no-play-mode launch preview for a selected <see cref="PhysicsForceClip" />. When a Force clip
    ///     is selected in the Timeline window, this draws — in the Scene view — the ballistic arc the bound
    ///     <see cref="PhysicsBodyAuthoring" /> would fly along if the clip fired now: launch velocity, the trajectory
    ///     under gravity, the apex, and an approximate landing point on the launch plane.
    ///
    ///     It is intentionally an APPROXIMATION, not the simulation: it integrates a point mass and ignores
    ///     collisions, friction, damping, and any runtime stat multiplier. It is faithful to the parts that decide the
    ///     shape of the arc — the impulse model (Δv = force / mass, exactly what the force accumulator applies), the
    ///     body's mass, gravity factor, and initial velocity — so a designer tuning a "kick / launch" reads the right
    ///     direction and roughly the right distance without entering Play mode.
    ///
    ///     Only the FixedVector direction mode is previewable at edit time; the Toward/Random/Velocity modes resolve
    ///     against runtime entities/velocities, so for those we draw only an honest "resolves at runtime" marker.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysicsForceTrajectoryPreview
    {
        private const string MenuPath = "BovineLabs/Physics/Preview Force Trajectories";
        private const string EnabledKey = "BovineLabs.Physics.ForceTrajectoryPreview";

        // ponytail: constant world gravity (Unity.Physics default), scaled per-body by GravityFactor. A scene that
        // overrides PhysicsStep gravity won't be matched — add a scene read if a project ever needs it.
        private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

        private const float Dt = 0.02f;        // 50 Hz, the usual fixed step
        private const float Horizon = 5f;       // seconds of flight to integrate at most
        private const float MinLaunchSpeed = 0.01f;

        private static readonly Color LaunchColor = new(0.95f, 0.55f, 0.2f);
        private static readonly Color ArcColor = new(0.95f, 0.75f, 0.35f);
        private static readonly Color MarkColor = new(0.4f, 0.85f, 0.95f);
        private static readonly Color MutedColor = new(0.6f, 0.6f, 0.6f);

        private static GUIStyle labelStyle;

        static PhysicsForceTrajectoryPreview()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnSceneGui(SceneView _)
        {
            if (!Enabled)
            {
                return;
            }

            var director = TimelineEditor.inspectedDirector;
            var clips = TimelineEditor.selectedClips;
            if (director == null || clips == null || clips.Length == 0)
            {
                return;
            }

            // One preview per bound body keeps overlapping labels readable when several clips are selected.
            var drawn = new HashSet<PhysicsBodyAuthoring>();
            foreach (var clip in clips)
            {
                if (clip?.asset is not PhysicsForceClip force)
                {
                    continue;
                }

                var track = clip.GetParentTrack();
                if (track == null || director.GetGenericBinding(track) is not PhysicsBodyAuthoring body)
                {
                    continue;
                }

                if (drawn.Add(body))
                {
                    DrawPreview(force, body);
                }
            }
        }

        private static void DrawPreview(PhysicsForceClip force, PhysicsBodyAuthoring body)
        {
            var origin = body.transform.position;
            var size = HandleUtility.GetHandleSize(origin);

            Handles.color = LaunchColor;
            Handles.SphereHandleCap(0, origin, Quaternion.identity, size * 0.08f, EventType.Repaint);

            if (force.directionMode != PhysicsForceDirectionMode.FixedVector)
            {
                Handles.color = MutedColor;
                Label(origin + (Vector3.up * size * 0.25f),
                    $"Force '{force.directionMode}' resolves at runtime — no edit-time arc");
                return;
            }

            var mass = body.Mass; // PositiveInfinity unless the body is Dynamic
            if (float.IsInfinity(mass))
            {
                Handles.color = MutedColor;
                Label(origin + (Vector3.up * size * 0.25f), "Body is not Dynamic — a force won't move it");
                return;
            }

            var worldForce = ResolveFixedVector(force, body);
            var launchVelocity = (Vector3)body.InitialLinearVelocity + (worldForce / mass);
            var speed = launchVelocity.magnitude;
            if (speed < MinLaunchSpeed)
            {
                Handles.color = MutedColor;
                Label(origin + (Vector3.up * size * 0.25f), "No launch force");
                return;
            }

            DrawLaunchArrow(origin, launchVelocity, size);

            var gravity = Gravity * body.GravityFactor;
            var path = Integrate(origin, launchVelocity, gravity, out var apex, out var landing, out var airtime);

            Handles.color = ArcColor;
            Handles.DrawAAPolyLine(4f, path.ToArray());

            Handles.color = MarkColor;
            if (apex.y > origin.y + (size * 0.05f))
            {
                Handles.DrawWireDisc(apex, Vector3.up, size * 0.06f);
                Label(apex + (Vector3.up * size * 0.12f), $"apex +{apex.y - origin.y:0.0} m");
            }

            var notes = Notes(force);
            if (landing.HasValue)
            {
                var range = Vector3.ProjectOnPlane(landing.Value - origin, Vector3.up).magnitude;
                Handles.DrawWireDisc(landing.Value, Vector3.up, size * 0.12f);
                Label(landing.Value + (Vector3.up * size * 0.18f),
                    $"≈ {range:0.0} m · {airtime:0.0} s\nlaunch {speed:0.0} m/s{notes}");
            }
            else
            {
                var tip = path[path.Count - 1];
                Label(tip + (Vector3.up * size * 0.15f), $"launch {speed:0.0} m/s{notes}");
            }
        }

        private static Vector3 ResolveFixedVector(PhysicsForceClip force, PhysicsBodyAuthoring body)
        {
            // Mirrors PhysicsMath.ResolveSpaceVector for the two frames knowable at edit time: None = world,
            // Self = the body's own rotation. Owner/Source/Target need runtime entities, so fall back to world.
            var rotation = force.space == Target.Self ? body.transform.rotation : Quaternion.identity;
            return rotation * force.linearForce;
        }

        private static void DrawLaunchArrow(Vector3 origin, Vector3 velocity, float size)
        {
            var dir = velocity.normalized;
            var tip = origin + (dir * size);
            Handles.color = LaunchColor;
            Handles.DrawAAPolyLine(5f, origin, tip);
            Handles.ConeHandleCap(0, tip, Quaternion.LookRotation(dir), size * 0.18f, EventType.Repaint);
        }

        private static List<Vector3> Integrate(
            Vector3 origin, Vector3 velocity, Vector3 gravity,
            out Vector3 apex, out Vector3? landing, out float airtime)
        {
            var points = new List<Vector3>(256) { origin };
            var p = origin;
            var v = velocity;
            apex = origin;
            landing = null;
            airtime = 0f;

            // A flat-plane landing only reads honestly for an upward launch; a horizontal/downward kick has no
            // ground estimate without scene geometry, so we just draw the arc to the horizon for those.
            // ponytail: ground assumed at the launch height — good enough to gauge range, not a collision query.
            var canLand = velocity.y > MinLaunchSpeed && gravity.y < 0f;
            var steps = Mathf.CeilToInt(Horizon / Dt);

            for (var i = 0; i < steps; i++)
            {
                var prev = p;
                v += gravity * Dt;
                p += v * Dt;
                points.Add(p);

                if (p.y > apex.y)
                {
                    apex = p;
                }

                if (canLand && prev.y >= origin.y && p.y < origin.y)
                {
                    var t = (origin.y - prev.y) / (p.y - prev.y);
                    var hit = Vector3.Lerp(prev, p, t);
                    points[points.Count - 1] = hit;
                    landing = hit;
                    airtime = (i + t) * Dt;
                    break;
                }
            }

            return points;
        }

        private static string Notes(PhysicsForceClip force)
        {
            var notes = string.Empty;
            if (force.mode == PhysicsForceMode.Continuous)
            {
                notes += " · continuous (shown as one tick)";
            }

            if (force.space != Target.Self && force.space != Target.None)
            {
                notes += $" · {force.space} space approximated as world";
            }

            return notes;
        }

        private static void Label(Vector3 position, string text)
        {
            labelStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                richText = false,
            };
            Handles.Label(position, text, labelStyle);
        }
    }
}
#endif
