#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    [CustomEditor(typeof(PhysicsLinearPIDClip))]
    public class PhysicsLinearPIDClipEditor : UnityEditor.Editor
    {
        private bool showPresets = true;
        private bool showTuning = true;
        private bool showAdvanced = false;

        private static readonly PIDPreset[] Presets =
        {
            new("🚀 Snappy",    "Fast, direct, slight overshoot.",    p:15f, i:1f,  d:3f,  limit:150f),
            new("🎯 Precise",   "Reaches goal accurately, no drift.", p:10f, i:3f,  d:2f,  limit:100f),
            new("🪶 Floaty",    "Slow and dreamy, lots of glide.",    p:4f,  i:0.5f,d:1f,  limit:40f),
            new("🏋️ Heavy",    "High force, strong damping.",        p:20f, i:2f,  d:8f,  limit:300f),
        };

        public override void OnInspectorGUI()
        {
            var clip = (PhysicsLinearPIDClip)target;
            serializedObject.Update();

            showPresets = EditorGUILayout.Foldout(showPresets, "✦ Presets", true, EditorStyles.foldoutHeader);
            if (showPresets) DrawPresets(clip);

            EditorGUILayout.Space(6);
            showTuning = EditorGUILayout.Foldout(showTuning, "✦ Quick Tuning", true, EditorStyles.foldoutHeader);
            if (showTuning) DrawTuningRows(clip);

            EditorGUILayout.Space(6);
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "✦ Raw Values", true, EditorStyles.foldoutHeader);
            if (showAdvanced)
            {
                DrawDefaultInspector();
            }
            else
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.chaseTargetBlend)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.localTargetOffset)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.maxForce)));
                
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.uniformAxes)));
                DrawPIDField(clip, nameof(PhysicsLinearPIDClip.proportional), "Proportional (P)");
                DrawPIDField(clip, nameof(PhysicsLinearPIDClip.integral),     "Integral (I)");
                DrawPIDField(clip, nameof(PhysicsLinearPIDClip.derivative),   "Derivative (D)");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresets(PhysicsLinearPIDClip clip)
        {
            var cols = 2;
            for (var i = 0; i < Presets.Length; i++)
            {
                if (i % cols == 0) EditorGUILayout.BeginHorizontal();

                var preset = Presets[i];
                if (GUILayout.Button(new GUIContent(preset.Label, preset.Tooltip)))
                {
                    Undo.RecordObject(clip, $"Apply Preset: {preset.Label}");
                    clip.proportional = new Vector3(preset.P, preset.P, preset.P);
                    clip.integral     = new Vector3(preset.I, preset.I, preset.I);
                    clip.derivative   = new Vector3(preset.D, preset.D, preset.D);
                    clip.maxForce     = preset.Limit;
                    EditorUtility.SetDirty(clip);
                }

                if (i % cols == cols - 1 || i == Presets.Length - 1) EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTuningRows(PhysicsLinearPIDClip clip)
        {
            DrawTuningRow(clip, "Too Sluggish", "Increase P.", 1.2f, 1f, 1f);
            DrawTuningRow(clip, "Too Twitchy", "Decrease P.", 0.85f, 1f, 1f);
            DrawTuningRow(clip, "Fix Overshoot", "Increase D, lower P.", 0.9f, 1f, 1.3f);
            DrawTuningRow(clip, "Fix Oscillation", "Reduce I, boost D.", 1f, 0.75f, 1.2f);
            DrawTuningRow(clip, "Fix Drift", "Increase I.", 1f, 1.25f, 1f);
        }

        private void DrawTuningRow(PhysicsLinearPIDClip clip, string label, string tooltip, float p, float i, float d)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Apply", GUILayout.Width(54)))
            {
                Undo.RecordObject(clip, $"PID Tune: {label}");
                clip.proportional *= p;
                clip.integral *= i;
                clip.derivative *= d;
                EditorUtility.SetDirty(clip);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPIDField(PhysicsLinearPIDClip clip, string propName, string displayName)
        {
            var prop = serializedObject.FindProperty(propName);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(prop, new GUIContent(displayName));
            if (EditorGUI.EndChangeCheck() && clip.uniformAxes)
            {
                var x = prop.FindPropertyRelative("x").floatValue;
                prop.FindPropertyRelative("y").floatValue = x;
                prop.FindPropertyRelative("z").floatValue = x;
            }
        }

        private readonly struct PIDPreset
        {
            public readonly string Label;
            public readonly string Tooltip;
            public readonly float P, I, D, Limit;

            public PIDPreset(string label, string tooltip, float p, float i, float d, float limit)
            {
                Label = label; Tooltip = tooltip; P = p; I = i; D = d; Limit = limit;
            }
        }
    }
}
#endif