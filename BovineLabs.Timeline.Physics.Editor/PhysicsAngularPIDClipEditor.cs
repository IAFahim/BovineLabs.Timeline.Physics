#if UNITY_EDITOR
using UnityEditor;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    [CustomEditor(typeof(PhysicsAngularPIDClip))]
    public class PhysicsAngularPIDClipEditor : UnityEditor.Editor
    {
        private bool showAdvanced;
        private bool showPresets = true;
        private bool showTuning = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var tuningProp = serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.tuning));

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
                    serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.trackingTarget)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetMode)));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetRotationEuler)));

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
                PidEditorUtility.DrawPIDFields(tuningProp,
                    serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.uniformAxes)));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif