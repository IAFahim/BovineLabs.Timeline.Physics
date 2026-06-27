#if UNITY_EDITOR
using System.Collections.Generic;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Authoring;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Editor
{
    [InitializeOnLoad]
    public static class PhysicsForceTrajectoryPreview
    {
        private const string MenuPath = "BovineLabs/Physics/Preview Force Trajectories";
        private const string EnabledKey = "BovineLabs.Physics.ForceTrajectoryPreview";

        private const float Dt = 0.02f;
        private const float Horizon = 5f;
        private const float MinLaunchSpeed = 0.01f;

        private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

        private static readonly Color LaunchColor = new(0.95f, 0.55f, 0.2f);
        private static readonly Color ArcColor = new(0.95f, 0.75f, 0.35f);
        private static readonly Color MarkColor = new(0.4f, 0.85f, 0.95f);
        private static readonly Color MutedColor = new(0.6f, 0.6f, 0.6f);

        private static GUIStyle labelStyle;

        static PhysicsForceTrajectoryPreview()
        {
            SceneView.duringSceneGui += OnSceneGui;
            // ponytail: CoreCLR/no-domain-reload — drop this sub before the assembly unloads or it accumulates per recompile. Upgrade path: [OnCodeUnloading].
            AssemblyReloadEvents.beforeAssemblyReload += () => SceneView.duringSceneGui -= OnSceneGui;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnSceneGui(SceneView _)
        {
            if (!Enabled) return;

            var director = TimelineEditor.inspectedDirector;
            var clips = TimelineEditor.selectedClips;
            if (director == null || clips == null || clips.Length == 0) return;

            var drawn = new HashSet<PhysicsBodyAuthoring>();
            foreach (var clip in clips)
            {
                if (clip?.asset is not PhysicsForceClip force) continue;

                var track = clip.GetParentTrack();
                if (track == null || director.GetGenericBinding(track) is not PhysicsBodyAuthoring body) continue;

                if (drawn.Add(body)) DrawPreview(force, body);
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
                DrawNonFixed(force, body, origin, size);
                return;
            }

            var mass = body.Mass;
            if (float.IsInfinity(mass))
            {
                Handles.color = MutedColor;
                Label(origin + Vector3.up * size * 0.25f, "Body is not Dynamic — a force won't move it");
                return;
            }

            var worldForce = ResolveFixedVector(force, body);
            var launchVelocity = (Vector3)body.InitialLinearVelocity + worldForce / mass;
            var speed = launchVelocity.magnitude;
            if (speed < MinLaunchSpeed)
            {
                Handles.color = MutedColor;
                Label(origin + Vector3.up * size * 0.25f, "No launch force");
                return;
            }

            DrawLaunchArrow(origin, launchVelocity, size);

            var gravity = Gravity * body.GravityFactor;
            var path = Integrate(origin, launchVelocity, gravity, out var apex, out var landing, out var airtime);

            Handles.color = ArcColor;
            Handles.DrawAAPolyLine(4f, path.ToArray());

            Handles.color = MarkColor;
            if (apex.y > origin.y + size * 0.05f)
            {
                Handles.DrawWireDisc(apex, Vector3.up, size * 0.06f);
                Label(apex + Vector3.up * size * 0.12f, $"apex +{apex.y - origin.y:0.0} m");
            }

            var notes = Notes(force);
            if (landing.HasValue)
            {
                var range = Vector3.ProjectOnPlane(landing.Value - origin, Vector3.up).magnitude;
                Handles.DrawWireDisc(landing.Value, Vector3.up, size * 0.12f);
                Label(landing.Value + Vector3.up * size * 0.18f,
                    $"≈ {range:0.0} m · {airtime:0.0} s\nlaunch {speed:0.0} m/s{notes}");
            }
            else
            {
                var tip = path[path.Count - 1];
                Label(tip + Vector3.up * size * 0.15f, $"launch {speed:0.0} m/s{notes}");
            }
        }

        // Modes whose direction is only known at runtime (Target/random/velocity). We still draw whatever the clip
        // data alone defines (cone shape, sphere, velocity arrow) plus a full data label, instead of an empty note.
        private static void DrawNonFixed(PhysicsForceClip force, PhysicsBodyAuthoring body, Vector3 origin, float size)
        {
            var arrowLen = size * 1.5f;
            var basis = force.space == Target.Self ? body.transform.rotation : Quaternion.identity;

            switch (force.directionMode)
            {
                case PhysicsForceDirectionMode.RandomSphere:
                    Handles.color = ArcColor;
                    Handles.DrawWireDisc(origin, Vector3.up, arrowLen);
                    Handles.DrawWireDisc(origin, Vector3.right, arrowLen);
                    Handles.DrawWireDisc(origin, Vector3.forward, arrowLen);
                    break;

                case PhysicsForceDirectionMode.RandomCone:
                    DrawCone(force, basis, origin, arrowLen);
                    break;

                case PhysicsForceDirectionMode.AlongVelocity:
                case PhysicsForceDirectionMode.AgainstVelocity:
                {
                    var v = (Vector3)body.InitialLinearVelocity;
                    if (v.sqrMagnitude > 1e-6f)
                    {
                        var dir = v.normalized;
                        if (force.directionMode == PhysicsForceDirectionMode.AgainstVelocity) dir = -dir;
                        DrawArrow(origin, dir * arrowLen, LaunchColor, size);
                    }
                    else
                    {
                        Handles.color = MutedColor;
                        Label(origin + Vector3.up * size * 0.4f, "Body has no Initial Linear Velocity to align to");
                    }

                    break;
                }

                case PhysicsForceDirectionMode.TowardTarget:
                case PhysicsForceDirectionMode.AwayFromTarget:
                    Handles.color = MutedColor;
                    Label(origin + Vector3.up * size * 0.4f,
                        $"'{force.directionMode}' aims at runtime '{force.directionTarget}' — no edit-time position");
                    break;
            }

            Handles.color = MutedColor;
            Label(origin + Vector3.up * size * 0.25f, DataLabel(force));
        }

        private static void DrawCone(PhysicsForceClip force, Quaternion basis, Vector3 origin, float len)
        {
            DrawConeRay(basis, force.coneAzimuthCenter, force.coneElevationCenter, origin, len, LaunchColor);

            var az = force.coneAzimuthHalfRange;
            var el = force.coneElevationHalfRange;
            DrawConeRay(basis, force.coneAzimuthCenter + az, force.coneElevationCenter + el, origin, len, ArcColor);
            DrawConeRay(basis, force.coneAzimuthCenter - az, force.coneElevationCenter + el, origin, len, ArcColor);
            DrawConeRay(basis, force.coneAzimuthCenter + az, force.coneElevationCenter - el, origin, len, ArcColor);
            DrawConeRay(basis, force.coneAzimuthCenter - az, force.coneElevationCenter - el, origin, len, ArcColor);
        }

        private static void DrawConeRay(Quaternion basis, float azDeg, float elDeg, Vector3 origin, float len,
            Color color)
        {
            var az = azDeg * Mathf.Deg2Rad;
            var el = elDeg * Mathf.Deg2Rad;
            var cosEl = Mathf.Cos(el);
            var local = new Vector3(cosEl * Mathf.Sin(az), Mathf.Sin(el), cosEl * Mathf.Cos(az));
            var dir = basis * local;
            Handles.color = color;
            Handles.DrawAAPolyLine(4f, origin, origin + dir * len);
        }

        private static void DrawArrow(Vector3 origin, Vector3 vector, Color color, float size)
        {
            var dir = vector.normalized;
            var tip = origin + vector;
            Handles.color = color;
            Handles.DrawAAPolyLine(5f, origin, tip);
            Handles.ConeHandleCap(0, tip, Quaternion.LookRotation(dir), size * 0.18f, EventType.Repaint);
        }

        private static string DataLabel(PhysicsForceClip force)
        {
            var mag = force.directionMode == PhysicsForceDirectionMode.FixedVector
                ? ((Vector3)force.linearForce).magnitude
                : force.magnitude;
            var modeTag = force.mode == PhysicsForceMode.Impulse ? "impulse" : "continuous";
            var label = $"{force.directionMode} · {modeTag} · mag {mag:0.#} · {force.space} space";
            if (force.latchDirection) label += " · latched";
            if (force.resetVelocityOnFire != VelocityResetFlags.None) label += $" · reset {force.resetVelocityOnFire}";
            return label;
        }

        private static Vector3 ResolveFixedVector(PhysicsForceClip force, PhysicsBodyAuthoring body)
        {
            var rotation = force.space == Target.Self ? body.transform.rotation : Quaternion.identity;
            return rotation * force.linearForce;
        }

        private static void DrawLaunchArrow(Vector3 origin, Vector3 velocity, float size)
        {
            var dir = velocity.normalized;
            var tip = origin + dir * size;
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

            var canLand = velocity.y > MinLaunchSpeed && gravity.y < 0f;
            var steps = Mathf.CeilToInt(Horizon / Dt);

            for (var i = 0; i < steps; i++)
            {
                var prev = p;
                v += gravity * Dt;
                p += v * Dt;
                points.Add(p);

                if (p.y > apex.y) apex = p;

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
            if (force.mode == PhysicsForceMode.Continuous) notes += " · continuous (shown as one tick)";

            if (force.space != Target.Self && force.space != Target.None)
                notes += $" · {force.space} space approximated as world";

            return notes;
        }

        private static void Label(Vector3 position, string text)
        {
            labelStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                normal = { textColor = Color.white },
                richText = false
            };
            Handles.Label(position, text, labelStyle);
        }
    }
}
#endif