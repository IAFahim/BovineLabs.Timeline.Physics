#if UNITY_EDITOR
using UnityEditor;

namespace BovineLabs.Timeline.Physics.Authoring.Editor
{
    [CustomEditor(typeof(PhysicsAngularPIDClip))]
    public class PhysicsAngularPIDClipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var tuningProp  = serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.tuning));
            var uniformProp = serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.uniformAxes));

            // ── Gains ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
            PidEditorUtility.DrawGains(target, tuningProp, uniformProp);

            EditorGUILayout.Space(4);

            // ── Presets ───────────────────────────────────────────────────
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            PidEditorUtility.DrawPresets(target, tuningProp);

            EditorGUILayout.Space(6);

            // ── Destination ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.trackingTarget)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetMode)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsAngularPIDClip.targetRotationEuler)));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
