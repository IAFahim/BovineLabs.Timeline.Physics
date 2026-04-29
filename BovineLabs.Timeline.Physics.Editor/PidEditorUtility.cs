#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    public static class PidEditorUtility
    {
        private static readonly PIDPreset[] Presets =
        {
            new("Snappy", "Fast with slight overshoot", 20f, 4f, 0.5f, 200f),
            new("Balanced", "Smooth, no overshoot", 10f, 3f, 1f, 100f),
            new("Floaty", "Gentle, large overshoot", 4f, 0.5f, 0.2f, 40f),
            new("Heavy", "High force, well-damped", 30f, 10f, 1f, 400f),
            new("Precise", "Slow but kills drift", 8f, 4f, 5f, 80f),
            new("Rigid", "Near-kinematic feel", 60f, 20f, 0f, 1000f)
        };

        // ── Public API ────────────────────────────────────────────────────

        public static void DrawStrength(SerializedProperty strengthProp)
        {
            EditorGUILayout.PropertyField(strengthProp,
                new GUIContent("Strength",
                    "Output force multiplier. 0 = no effect, 1 = full PID force, >1 = amplified.\n" +
                    "Useful for blending PID in/out via Timeline clip weight or curves."));
        }

        public static void DrawGains(Object target, SerializedProperty tuning, SerializedProperty uniform)
        {
            uniform.boolValue = EditorGUILayout.Toggle(
                new GUIContent("Uniform Axes", "Keep X/Y/Z gains identical"),
                uniform.boolValue);

            var isUniform = uniform.boolValue;

            DrawGain(target, tuning, "Proportional", "P — Response",
                "How hard the controller pushes. Raise until it reaches the goal.", isUniform);
            DrawGain(target, tuning, "Derivative", "D — Damping",
                "Kills oscillation. Raise after P until stable. Too high = sluggish.", isUniform);
            DrawGain(target, tuning, "Integral", "I — Drift correction",
                "Only add if the entity stalls short of the goal. Too high = slow oscillation.", isUniform);

            EditorGUILayout.PropertyField(
                tuning.FindPropertyRelative("MaxOutput"),
                new GUIContent("Max Force", "Hard cap on output each frame. Prevents explosive behaviour."));
        }

        public static void DrawPresets(Object target, SerializedProperty tuning)
        {
            var curP = tuning.FindPropertyRelative("Proportional").FindPropertyRelative("x").floatValue;
            var curD = tuning.FindPropertyRelative("Derivative").FindPropertyRelative("x").floatValue;
            var curI = tuning.FindPropertyRelative("Integral").FindPropertyRelative("x").floatValue;

            const int cols = 3;
            for (var i = 0; i < Presets.Length; i++)
            {
                if (i % cols == 0) EditorGUILayout.BeginHorizontal();

                var p = Presets[i];
                var active = Mathf.Approximately(curP, p.P)
                             && Mathf.Approximately(curD, p.D)
                             && Mathf.Approximately(curI, p.I);

                using (new EditorGUI.DisabledScope(active))
                {
                    var label = active ? $"✓ {p.Name}" : p.Name;
                    var tip = $"{p.Description}\nP={p.P}  D={p.D}  I={p.I}  Max={p.Limit}";
                    if (GUILayout.Button(new GUIContent(label, tip),
                            EditorStyles.miniButton, GUILayout.ExpandWidth(true)))
                    {
                        Undo.RecordObject(target, $"PID Preset: {p.Name}");
                        ApplyPreset(tuning, p);
                        tuning.serializedObject.ApplyModifiedProperties();
                    }
                }

                if (i % cols == cols - 1 || i == Presets.Length - 1) EditorGUILayout.EndHorizontal();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void DrawGain(Object target, SerializedProperty tuning,
            string field, string label, string tooltip, bool uniform)
        {
            var prop = tuning.FindPropertyRelative(field);
            if (uniform)
            {
                EditorGUI.BeginChangeCheck();
                var v = EditorGUILayout.FloatField(new GUIContent(label, tooltip),
                    prop.FindPropertyRelative("x").floatValue);
                if (EditorGUI.EndChangeCheck())
                {
                    v = Mathf.Max(0f, v);
                    Undo.RecordObject(target, $"PID {field}");
                    SetFloat3(prop, v, v, v);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
                if (EditorGUI.EndChangeCheck()) ClampFloat3(prop, 0f);
            }
        }

        private static void ApplyPreset(SerializedProperty tuning, PIDPreset p)
        {
            SetFloat3(tuning.FindPropertyRelative("Proportional"), p.P, p.P, p.P);
            SetFloat3(tuning.FindPropertyRelative("Derivative"), p.D, p.D, p.D);
            SetFloat3(tuning.FindPropertyRelative("Integral"), p.I, p.I, p.I);
            tuning.FindPropertyRelative("MaxOutput").floatValue = p.Limit;
        }

        private static void SetFloat3(SerializedProperty prop, float x, float y, float z)
        {
            prop.FindPropertyRelative("x").floatValue = x;
            prop.FindPropertyRelative("y").floatValue = y;
            prop.FindPropertyRelative("z").floatValue = z;
        }

        private static void ClampFloat3(SerializedProperty prop, float min)
        {
            var px = prop.FindPropertyRelative("x");
            var py = prop.FindPropertyRelative("y");
            var pz = prop.FindPropertyRelative("z");
            px.floatValue = Mathf.Max(min, px.floatValue);
            py.floatValue = Mathf.Max(min, py.floatValue);
            pz.floatValue = Mathf.Max(min, pz.floatValue);
        }

        private readonly struct PIDPreset
        {
            public readonly string Name, Description;
            public readonly float P, D, I, Limit;

            public PIDPreset(string name, string description, float p, float d, float i, float limit)
            {
                (Name, Description, P, D, I, Limit) = (name, description, p, d, i, limit);
            }
        }
    }
}
#endif