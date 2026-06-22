using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Physics.Authoring;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    [CustomEditor(typeof(SweptTriggerSourceAuthoring))]
    public sealed class SweptTriggerSourceAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var src = (SweptTriggerSourceAuthoring)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Swept Trigger — Setup Check", EditorStyles.boldLabel);

            var shape = src.GetComponent<PhysicsShapeAuthoring>();

            if (shape == null)
            {
                EditorGUILayout.HelpBox(
                    "No PhysicsShapeAuthoring — the swept volume is defined by a (disabled) PhysicsShapeAuthoring " +
                    "(Box / Capsule / Sphere / Cylinder / Convex / Mesh) with Unity's shape handles.",
                    MessageType.Error);
                if (GUILayout.Button("Fix: add a disabled PhysicsShapeAuthoring (capsule) to define the volume"))
                    AddDisabledShape(src.gameObject);
            }
            else
            {
                if (shape.enabled)
                {
                    EditorGUILayout.HelpBox(
                        "The PhysicsShapeAuthoring is ENABLED, so it ALSO becomes a real collider. Disable it — the " +
                        "swept source only READS its shape; it must not be a physics body.",
                        MessageType.Error);
                    if (GUILayout.Button(
                            "Fix: disable the PhysicsShapeAuthoring (keep its shape, drop the real collider)"))
                    {
                        Undo.RecordObject(shape, "Disable swept shape");
                        shape.enabled = false;
                        EditorUtility.SetDirty(shape);
                    }
                }

                if (shape.CollidesWith.Value == 0)
                    EditorGUILayout.HelpBox(
                        "The shape's 'Collides With' is empty — the sweep will hit NOTHING. Set it on the " +
                        "PhysicsShapeAuthoring (Override Collides With) to your enemy category.",
                        MessageType.Warning);
            }

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
                EditorGUILayout.HelpBox(
                    "TargetsAuthoring present but Owner is empty — set it to the wielder so the swing ignores it.",
                    MessageType.Warning);
                if (GUILayout.Button("Fix: set Owner = rig root"))
                {
                    Undo.RecordObject(targets, "Set Owner");
                    targets.Owner = src.transform.root != null ? src.transform.root.gameObject : src.gameObject;
                    EditorUtility.SetDirty(targets);
                }
            }

            EditorGUILayout.HelpBox(
                "Bind a 'BovineLabs/Physics/Swept Trigger' track to this component. On fast swings use trigger state ENTER (not Stay). " +
                "Size/orient the volume with the PhysicsShapeAuthoring's Scene handles; enable the 'sweptgizmo.draw-enabled' " +
                "ConfigVar to see the swept bounds in play.",
                MessageType.Info);
        }

        [MenuItem("GameObject/BovineLabs/Physics/Swept Trigger Source", false, 30)]
        private static void AddSweptSource(MenuCommand cmd)
        {
            var go = cmd.context as GameObject ?? Selection.activeGameObject;
            if (go == null) return;

            if (go.GetComponent<SweptTriggerSourceAuthoring>() == null)
                Undo.AddComponent<SweptTriggerSourceAuthoring>(go);

            if (go.GetComponent<PhysicsShapeAuthoring>() == null) AddDisabledShape(go);

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

        private static void AddDisabledShape(GameObject go)
        {
            var shape = Undo.AddComponent<PhysicsShapeAuthoring>(go);
            shape.SetCapsule(new CapsuleGeometryAuthoring
            {
                Center = float3.zero,
                Height = 0.8f,
                Radius = 0.1f,
                Orientation = quaternion.identity
            });
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = new PhysicsCategoryTags { Value = ~0u };
            shape.enabled = false;
            EditorUtility.SetDirty(shape);
        }
    }
}
#endif