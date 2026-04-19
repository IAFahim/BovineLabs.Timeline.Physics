#if UNITY_EDITOR
using UnityEditor;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    [CustomEditor(typeof(PhysicsLinearPIDClip))]
    public class PhysicsLinearPIDClipEditor : UnityEditor.Editor
    {
        private bool showAdvanced;
        private bool showPresets = true;
        private bool showTuning = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var tuningProp = serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.tuning));

            showPresets = EditorGUILayout.Foldout(showPresets, "✦ Presets", true, EditorStyles.foldoutHeader);
            if (showPresets) PidEditorUtility.DrawPresets(target, tuningProp);

            EditorGUILayout.Space(6);
            showTuning = EditorGUILayout.Foldout(showTuning, "✦ Quick Tuning", true, EditorStyles.foldoutHeader);
            if (showTuning) PidEditorUtility.DrawTuningRows(target, tuningProp);

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
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.trackingTarget)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.targetMode)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.targetOffset)));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
                PidEditorUtility.DrawPIDFields(tuningProp,
                    serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.uniformAxes)));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif