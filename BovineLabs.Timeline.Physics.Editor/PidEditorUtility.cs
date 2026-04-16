#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    public static class PidEditorUtility
    {
        private static readonly PIDPreset[] Presets =
        {
            new("🚀 Snappy",    "Fast, direct, slight overshoot.",    p:15f, i:1f,  d:3f,  limit:150f),
            new("🎯 Precise",   "Reaches goal accurately, no drift.", p:10f, i:3f,  d:2f,  limit:100f),
            new("🪶 Floaty",    "Slow and dreamy, lots of glide.",    p:4f,  i:0.5f,d:1f,  limit:40f),
            new("🏋️ Heavy",    "High force, strong damping.",        p:20f, i:2f,  d:8f,  limit:300f),
        };

        public static void DrawPresets(Object target, SerializedProperty tuningProp)
        {
            EditorGUILayout.HelpBox("Pick starting point. Fine-tune after.", MessageType.None);
            var cols = 2;
            for (var i = 0; i < Presets.Length; i++)
            {
                if (i % cols == 0) EditorGUILayout.BeginHorizontal();

                var preset = Presets[i];
                if (GUILayout.Button(new GUIContent(preset.Label, preset.Tooltip)))
                {
                    Undo.RecordObject(target, $"Apply Preset: {preset.Label}");
                    tuningProp.FindPropertyRelative("Proportional").vector3Value = new Vector3(preset.P, preset.P, preset.P);
                    tuningProp.FindPropertyRelative("Integral").vector3Value     = new Vector3(preset.I, preset.I, preset.I);
                    tuningProp.FindPropertyRelative("Derivative").vector3Value   = new Vector3(preset.D, preset.D, preset.D);
                    tuningProp.FindPropertyRelative("MaxOutput").floatValue      = preset.Limit;
                    tuningProp.serializedObject.ApplyModifiedProperties();
                }

                if (i % cols == cols - 1 || i == Presets.Length - 1) EditorGUILayout.EndHorizontal();
            }
        }

        public static void DrawTuningRows(Object target, SerializedProperty tuningProp)
        {
            DrawTuningRow(target, tuningProp, "Too Sluggish", "Increase P.", 1.2f, 1f, 1f);
            DrawTuningRow(target, tuningProp, "Too Twitchy", "Decrease P.", 0.85f, 1f, 1f);
            DrawTuningRow(target, tuningProp, "Fix Overshoot", "Increase D, lower P.", 0.9f, 1f, 1.3f);
            DrawTuningRow(target, tuningProp, "Fix Oscillation", "Reduce I, boost D.", 1f, 0.75f, 1.2f);
            DrawTuningRow(target, tuningProp, "Fix Drift", "Increase I.", 1f, 1.25f, 1f);
        }

        private static void DrawTuningRow(Object target, SerializedProperty prop, string label, string tooltip, float p, float i, float d)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Apply", GUILayout.Width(54)))
            {
                Undo.RecordObject(target, $"PID Tune: {label}");
                prop.FindPropertyRelative("Proportional").vector3Value *= p;
                prop.FindPropertyRelative("Integral").vector3Value *= i;
                prop.FindPropertyRelative("Derivative").vector3Value *= d;
                prop.serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawPIDFields(SerializedProperty tuningProp, SerializedProperty uniformAxesProp)
        {
            EditorGUILayout.PropertyField(uniformAxesProp);
            DrawField(tuningProp, "Proportional", uniformAxesProp.boolValue);
            DrawField(tuningProp, "Integral", uniformAxesProp.boolValue);
            DrawField(tuningProp, "Derivative", uniformAxesProp.boolValue);
            EditorGUILayout.PropertyField(tuningProp.FindPropertyRelative("MaxOutput"));
        }

        private static void DrawField(SerializedProperty tuningProp, string propName, bool uniform)
        {
            var prop = tuningProp.FindPropertyRelative(propName);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop);
            if (EditorGUI.EndChangeCheck() && uniform)
            {
                var x = prop.vector3Value.x;
                prop.vector3Value = new Vector3(x, x, x);
            }
        }

        private readonly struct PIDPreset
        {
            public readonly string Label;
            public readonly string Tooltip;
            public readonly float P, I, D, Limit;
            public PIDPreset(string label, string tooltip, float p, float i, float d, float limit) { Label = label; Tooltip = tooltip; P = p; I = i; D = d; Limit = limit; }
        }
    }
}
#endif
