#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    [CustomEditor(typeof(PhysicsPIDClip))]
    public class PhysicsPIDClipEditor : UnityEditor.Editor
    {
        // ── Foldout state ─────────────────────────────────────────────────────
        private bool showPresets  = true;
        private bool showTuning   = true;
        private bool showAdvanced = false;

        // ── Preset definitions ────────────────────────────────────────────────
        private static readonly PIDPreset[] Presets =
        {
            new("🚀 Snappy",    "Fast, direct, slight overshoot.",    p:15f, i:1f,  d:3f,  maxForce:150f),
            new("🎯 Precise",   "Reaches goal accurately, no drift.", p:10f, i:3f,  d:2f,  maxForce:100f),
            new("🪶 Floaty",    "Slow and dreamy, lots of glide.",    p:4f,  i:0.5f,d:1f,  maxForce:40f),
            new("🏋️ Heavy",    "High force, strong damping.",        p:20f, i:2f,  d:8f,  maxForce:300f),
            new("🎈 Balloon",   "Very soft, low gravity feel.",       p:2f,  i:0f,  d:0.5f,maxForce:20f),
        };

        public override void OnInspectorGUI()
        {
            var clip = (PhysicsPIDClip)target;
            serializedObject.Update();

            // ── Presets ───────────────────────────────────────────────────────
            showPresets = EditorGUILayout.Foldout(showPresets, "✦ Presets", true, EditorStyles.foldoutHeader);
            if (showPresets)
            {
                EditorGUILayout.HelpBox(
                    "Pick a starting point — you can fine-tune afterward.",
                    MessageType.None);

                var cols = 3;
                for (var i = 0; i < Presets.Length; i++)
                {
                    if (i % cols == 0) EditorGUILayout.BeginHorizontal();

                    var preset = Presets[i];
                    if (GUILayout.Button(new GUIContent(preset.Label, preset.Tooltip)))
                    {
                        Undo.RecordObject(clip, $"Apply PID Preset: {preset.Label}");
                        ApplyPreset(clip, preset);
                        EditorUtility.SetDirty(clip);
                    }

                    if (i % cols == cols - 1 || i == Presets.Length - 1) EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(6);

            // ── Tuning helpers ────────────────────────────────────────────────
            showTuning = EditorGUILayout.Foldout(showTuning, "✦ Quick Tuning", true, EditorStyles.foldoutHeader);
            if (showTuning)
            {
                EditorGUILayout.HelpBox(
                    "Each button nudges the values in the right direction. " +
                    "Apply repeatedly until the behaviour feels right.",
                    MessageType.None);

                DrawTuningRow(clip, "Too Slow / Sluggish",
                    "Increases Proportional — object accelerates harder toward the goal.",
                    () => ScaleAll(clip, p: 1.2f, i: 1f, d: 1f));

                DrawTuningRow(clip, "Too Fast / Twitchy",
                    "Decreases Proportional — calms the initial push.",
                    () => ScaleAll(clip, p: 0.85f, i: 1f, d: 1f));

                DrawTuningRow(clip, "Fix Overshoot / Bouncing",
                    "Increases Derivative (damping) and slightly lowers Proportional.",
                    () => ScaleAll(clip, p: 0.9f, i: 1f, d: 1.3f));

                DrawTuningRow(clip, "Fix Oscillation / Wobbling",
                    "Reduces Integral (stops error from building) and boosts Derivative.",
                    () => ScaleAll(clip, p: 1f, i: 0.75f, d: 1.2f));

                DrawTuningRow(clip, "Fix Drift / Never Arrives",
                    "Increases Integral — builds up a corrective nudge over time.",
                    () => ScaleAll(clip, p: 1f, i: 1.25f, d: 1f));

                DrawTuningRow(clip, "Soften Overall",
                    "Scales all gains down — generally calmer motion.",
                    () => ScaleAll(clip, p: 0.85f, i: 0.85f, d: 0.85f));

                DrawTuningRow(clip, "Strengthen Overall",
                    "Scales all gains up — more decisive motion.",
                    () => ScaleAll(clip, p: 1.2f, i: 1.2f, d: 1.2f));
            }

            EditorGUILayout.Space(6);

            // ── Standard fields ───────────────────────────────────────────────
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "✦ Raw Values", true, EditorStyles.foldoutHeader);
            if (showAdvanced)
            {
                DrawDefaultInspector();
            }
            else
            {
                // Always draw the non-PID fields so designers can still tweak
                // destination and maxForce without opening raw values.
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsPIDClip.chaseTargetBlend)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsPIDClip.localTargetOffset)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsPIDClip.maxForce)));
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Current PID Gains", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsPIDClip.uniformAxes)));
                DrawPIDField(clip, nameof(PhysicsPIDClip.proportional), "Proportional (P)");
                DrawPIDField(clip, nameof(PhysicsPIDClip.integral),     "Integral (I)");
                DrawPIDField(clip, nameof(PhysicsPIDClip.derivative),   "Derivative (D)");
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void DrawTuningRow(PhysicsPIDClip clip, string label, string tooltip, System.Action apply)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Apply", GUILayout.Width(54)))
            {
                Undo.RecordObject(clip, $"PID Tune: {label}");
                apply();
                EditorUtility.SetDirty(clip);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPIDField(PhysicsPIDClip clip, string propName, string displayName)
        {
            var prop = serializedObject.FindProperty(propName);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop, new GUIContent(displayName));
            if (EditorGUI.EndChangeCheck() && clip.uniformAxes)
            {
                // Mirror the first changed axis across all three
                var x = prop.FindPropertyRelative("x").floatValue;
                prop.FindPropertyRelative("y").floatValue = x;
                prop.FindPropertyRelative("z").floatValue = x;
            }
        }

        private static void ScaleAll(PhysicsPIDClip clip, float p, float i, float d)
        {
            clip.proportional = new Vector3(
                clip.proportional.x * p, clip.proportional.y * p, clip.proportional.z * p);
            clip.integral = new Vector3(
                clip.integral.x * i, clip.integral.y * i, clip.integral.z * i);
            clip.derivative = new Vector3(
                clip.derivative.x * d, clip.derivative.y * d, clip.derivative.z * d);
        }

        private static void ApplyPreset(PhysicsPIDClip clip, PIDPreset preset)
        {
            clip.proportional = new Vector3(preset.P, preset.P, preset.P);
            clip.integral     = new Vector3(preset.I, preset.I, preset.I);
            clip.derivative   = new Vector3(preset.D, preset.D, preset.D);
            clip.maxForce     = preset.MaxForce;
        }

        // ── Preset data ───────────────────────────────────────────────────────
        private readonly struct PIDPreset
        {
            public readonly string Label;
            public readonly string Tooltip;
            public readonly float P, I, D, MaxForce;

            public PIDPreset(string label, string tooltip, float p, float i, float d, float maxForce)
            {
                Label   = label;
                Tooltip = tooltip;
                P = p; I = i; D = d; MaxForce = maxForce;
            }
        }
    }
}
#endif