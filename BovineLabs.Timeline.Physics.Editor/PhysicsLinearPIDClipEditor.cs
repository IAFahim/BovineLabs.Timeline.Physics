using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Authoring.PIDs;
using UnityEditor;

#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    [CustomEditor(typeof(PhysicsLinearPIDClip))]
    public class PhysicsLinearPIDClipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var clip = (PhysicsLinearPIDClip)target;
            var tuningProp = serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.tuning));
            var uniformProp = serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.uniformAxes));

            EditorGUILayout.LabelField("Gains", EditorStyles.boldLabel);
            PidEditorUtility.DrawGains(target, tuningProp, uniformProp);

            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            PidEditorUtility.DrawPresets(target, tuningProp);

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.trackingTarget)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.targetMode)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.targetOffset)));

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Influence", EditorStyles.boldLabel);
            PidEditorUtility.DrawStrength(serializedObject.FindProperty(nameof(PhysicsLinearPIDClip.strength)));

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Stat Multiplier", EditorStyles.boldLabel);
            PidEditorUtility.DrawStatSection(serializedObject);

            if (clip.trackingTarget == Target.Self && clip.targetMode == PidLinearTargetMode.TargetLocal)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Self + TargetLocal: the goal moves with the entity — it will accelerate forever.\n" +
                    "Switch Target Mode to InitialLocal to lock the goal at the entity's starting position.",
                    MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif