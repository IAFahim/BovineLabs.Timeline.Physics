#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    using BovineLabs.Reaction.Authoring.Core;
    using BovineLabs.Timeline.Physics.Authoring;
    using Unity.Physics.Authoring;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Designer-facing inspector for <see cref="SweptTriggerSourceAuthoring"/>: surfaces the silent-failure
    /// traps inline (empty Collides With, missing Owner, bad size) with one-click fixes, plus a reminder of
    /// the Enter-not-Stay rule. Pairs with the author-time Scene gizmo on the component itself.
    /// </summary>
    [CustomEditor(typeof(SweptTriggerSourceAuthoring))]
    public sealed class SweptTriggerSourceAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();

            var src = (SweptTriggerSourceAuthoring)this.target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Swept Trigger — Setup Check", EditorStyles.boldLabel);

            // Trap #1: empty CollidesWith => hits nothing, no error.
            if (src.collidesWith.Value == 0)
            {
                EditorGUILayout.HelpBox("'Collides With' is empty — the sweep will hit NOTHING.", MessageType.Error);
                if (GUILayout.Button("Fix: set Collides With = Everything (then narrow to your enemy category)"))
                {
                    Undo.RecordObject(src, "Swept Collides With");
                    src.collidesWith = new PhysicsCategoryTags { Value = ~0u };
                    EditorUtility.SetDirty(src);
                }
            }

            // Trap #2: no Targets/Owner => clips' Ignore Target = Owner can't skip the wielder.
            var targets = src.GetComponent<TargetsAuthoring>();
            if (targets == null)
            {
                EditorGUILayout.HelpBox(
                    "No TargetsAuthoring on this weapon — a clip's Ignore Target = Owner can't skip the wielder, so the swing may hit the character itself.",
                    MessageType.Warning);
                if (GUILayout.Button("Fix: add TargetsAuthoring (Owner = rig root)"))
                {
                    var t = Undo.AddComponent<TargetsAuthoring>(src.gameObject);
                    t.Owner = src.transform.root != null ? src.transform.root.gameObject : src.gameObject;
                    t.Source = src.gameObject;
                    EditorUtility.SetDirty(t);
                }
            }
            else if (targets.Owner == null)
            {
                EditorGUILayout.HelpBox("TargetsAuthoring present but Owner is empty — set it to the wielder so the swing ignores it.", MessageType.Warning);
                if (GUILayout.Button("Fix: set Owner = rig root"))
                {
                    Undo.RecordObject(targets, "Set Owner");
                    targets.Owner = src.transform.root != null ? src.transform.root.gameObject : src.gameObject;
                    EditorUtility.SetDirty(targets);
                }
            }

            // Trap #3: degenerate capsule.
            if (src.radius <= 0.001f)
            {
                EditorGUILayout.HelpBox("Radius must be > 0. The blue capsule gizmo in the Scene view shows the swept volume.", MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Bind a 'BovineLabs/Physics/Swept Trigger' track to this component. On fast swings use trigger state ENTER (not Stay). " +
                "The Scene gizmo shows the capsule while this object is selected.",
                MessageType.Info);
        }

        /// <summary>One-click: add a configured swept source (+ Owner-set Targets) to the selected weapon object.</summary>
        [MenuItem("GameObject/BovineLabs/Physics/Swept Trigger Source", false, 30)]
        private static void AddSweptSource(MenuCommand cmd)
        {
            var go = cmd.context as GameObject ?? Selection.activeGameObject;
            if (go == null)
            {
                return;
            }

            var src = go.GetComponent<SweptTriggerSourceAuthoring>();
            if (src == null)
            {
                src = Undo.AddComponent<SweptTriggerSourceAuthoring>(go);
                src.collidesWith = new PhysicsCategoryTags { Value = ~0u };
                EditorUtility.SetDirty(src);
            }

            if (go.GetComponent<TargetsAuthoring>() == null)
            {
                var t = Undo.AddComponent<TargetsAuthoring>(go);
                t.Owner = go.transform.root != null ? go.transform.root.gameObject : go;
                t.Source = go;
                EditorUtility.SetDirty(t);
            }

            Selection.activeGameObject = go;
        }

        [MenuItem("GameObject/BovineLabs/Physics/Swept Trigger Source", true)]
        private static bool AddSweptSourceValidate(MenuCommand cmd)
        {
            return (cmd.context as GameObject ?? Selection.activeGameObject) != null;
        }
    }
}
#endif
