using BovineLabs.Timeline.Physics.Authoring.PIDs;
using UnityEditor;

#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    [CustomEditor(typeof(PhysicsAngularPIDClip))]
    public class PhysicsAngularPIDClipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tuningProp = serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.tuning));
            var uniformProp = serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.uniformAxes));

            EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
            PidEditorUtility.DrawGains(target, tuningProp, uniformProp);

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            PidEditorUtility.DrawPresets(target, tuningProp);

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.trackingTarget)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetMode)));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetRotationEuler)));

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Influence", EditorStyles.boldLabel);
            PidEditorUtility.DrawStrength(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.strength)));

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Stat Multiplier", EditorStyles.boldLabel);
            PidEditorUtility.DrawStatSection(serializedObject);

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Stop Threshold (Optional)", EditorStyles.boldLabel);
            PidEditorUtility.DrawStopThreshold(
                serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.stopThreshold)));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif