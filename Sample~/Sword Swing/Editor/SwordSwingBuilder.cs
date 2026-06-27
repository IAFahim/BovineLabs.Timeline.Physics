#if UNITY_EDITOR
using System;
using BovineLabs.Core.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core.Authoring;
using BovineLabs.Timeline.Physics;
using BovineLabs.Timeline.Physics.Authoring.PIDs;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SwordSwing.Editor
{
    /// <summary>
    ///     PHYSICS-based sword swing. A classic capsule "player" with a silly sword that swings around it in the XZ
    ///     plane (left → right) — driven by a package PHYSICS Timeline track, <see cref="PhysicsAngularPIDClip" />: a
    ///     dynamic pivot body at the capsule's centre (gravity off) is torqued toward two World yaw targets (−50° then
    ///     +50°) by the angular-PID motor (force via PendingForce, resolved through inverse inertia). The sword is
    ///     rigid to that body, so the body's physical rotation carries it around the player. No kinematic transform
    ///     writes — the rotation is a real torque. Builds into the active scene's SubScene.
    ///     Run via Tools ▸ Vex ▸ Build Sword Swing (Physics).
    /// </summary>
    public static class SwordSwingBuilder
    {
        private const string Root = "Sword Swing";
        private static readonly Vector3 Centre = new(12f, 1f, -5f);

        [MenuItem("Tools/Vex/Build Sword Swing (Physics)")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "BLOCKED|play mode";

            var parent = EditorSceneManager.GetActiveScene();
            var parentPath = parent.path;
            var subPath = FindSubScenePath(parent);
            if (string.IsNullOrEmpty(subPath)) return "BLOCKED|No SubScene in the active scene.";
            var sub = EditorSceneManager.OpenScene(subPath, OpenSceneMode.Additive);
            var log = new System.Text.StringBuilder();
            try
            {
                EditorSceneManager.SetActiveScene(sub);
                foreach (var g in sub.GetRootGameObjects())
                    if (g.name == Root)
                        UnityEngine.Object.DestroyImmediate(g);

                var root = new GameObject(Root);
                EditorSceneManager.MoveGameObjectToScene(root, sub);

                // --- player (static physics capsule) ---
                var player = Primitive(PrimitiveType.Capsule, "Player Capsule", Centre, new Vector3(1, 1, 1), root.transform);
                var pCap = player.AddComponent<PhysicsBodyAuthoring>();
                pCap.MotionType = BodyMotionType.Static;
                player.AddComponent<PhysicsShapeAuthoring>().SetBox(new Unity.Physics.BoxGeometry
                {
                    Center = Unity.Mathematics.float3.zero, Size = new Unity.Mathematics.float3(1f, 2f, 1f),
                    Orientation = Unity.Mathematics.quaternion.identity, BevelRadius = 0.05f,
                });

                // --- pivot: a DYNAMIC physics body, gravity off, torqued by the angular-PID motor ---
                var pivot = new GameObject("Sword Pivot");
                pivot.transform.SetParent(root.transform, true);
                pivot.transform.position = Centre;
                var body = pivot.AddComponent<PhysicsBodyAuthoring>();
                body.MotionType = BodyMotionType.Dynamic;
                body.Mass = 1f;
                body.GravityFactor = 0f;   // it only rotates in place (pure torque), never falls
                body.AngularDamping = 0f;
                var pivotShape = pivot.AddComponent<PhysicsShapeAuthoring>();
                pivotShape.SetSphere(
                    new Unity.Physics.SphereGeometry { Center = Unity.Mathematics.float3.zero, Radius = 0.25f },
                    Unity.Mathematics.quaternion.identity);
                // It only needs to rotate, not collide. It sits inside the player at the same point, so collide with
                // NOTHING — otherwise the solver ejects the overlapping bodies and launches the pivot.
                pivotShape.OverrideCollidesWith = true;
                pivotShape.CollidesWith = new PhysicsCategoryTags { Value = 0u };

                // --- the silly sword: rigid to the pivot (child mesh, offset radially) so the body's spin carries it ---
                var sword = Primitive(PrimitiveType.Cube, "Silly Sword", default, new Vector3(0.12f, 0.12f, 1.7f), pivot.transform);
                sword.transform.localPosition = new Vector3(0f, 0f, 1.25f);
                sword.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
                Dynamic(sword);
                var guard = Primitive(PrimitiveType.Cube, "Guard", default, new Vector3(0.45f, 0.12f, 0.12f), sword.transform);
                guard.transform.localPosition = new Vector3(0f, 0f, -0.45f);
                Dynamic(guard);

                // --- director + angular-PID physics track bound to the pivot body ---
                var directorGo = new GameObject("Sword Director");
                directorGo.transform.SetParent(root.transform, true);
                directorGo.transform.position = Centre;
                var director = directorGo.AddComponent<PlayableDirector>();

                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, "Assets/SwordSwing/SwordSwing.playable");
                var track = timeline.CreateTrack<PhysicsAngularPIDTrack>(null, "Sword Swing (torque)");

                // Two World-yaw targets with a long overlap: the angular PID chases a target that slerps −50° → +50°,
                // torquing the body left → right; the loop resets and repeats.
                Clip(track, -50f, 0.0, 2.0);
                Clip(track, 50f, 0.2, 2.0);

                director.playableAsset = timeline;
                director.playOnAwake = true;
                director.extrapolationMode = DirectorWrapMode.Loop;
                director.SetGenericBinding(track, body);
                directorGo.AddComponent<TimelinePlayTrigger>();

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(sub);
                EditorSceneManager.SaveScene(sub);
                log.AppendLine("BUILT|capsule + dynamic pivot body (gravity 0) + sword + PhysicsAngularPIDTrack (2 World-yaw clips), bound to pivot body");
            }
            catch (Exception e) { log.AppendLine("EXCEPTION|" + e.GetType().Name + ": " + e.Message); }
            finally
            {
                EditorSceneManager.SetActiveScene(parent);
                EditorSceneManager.CloseScene(sub, false);
                EditorSceneManager.OpenScene(parentPath, OpenSceneMode.Single);
            }
            return log.ToString();
        }

        private static void Clip(TrackAsset track, float yawDegrees, double start, double duration)
        {
            var clip = track.CreateClip<PhysicsAngularPIDClip>();
            clip.start = start;
            clip.duration = duration;
            var a = (PhysicsAngularPIDClip)clip.asset;
            a.trackingTarget = Target.Self;
            a.targetMode = PidAngularTargetMode.World;        // targetRotationEuler is the absolute world rotation
            a.targetRotationEuler = new Vector3(0f, yawDegrees, 0f);
            a.strength = 1f;
            EditorUtility.SetDirty(a);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        private static void Dynamic(GameObject go)
        {
            var ta = go.AddComponent<BovineLabs.Core.Authoring.TransformAuthoring>();
            ta.TransformUsageFlags = TransformUsageFlags.Dynamic;
        }

        private static string FindSubScenePath(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var ss = go.GetComponent<Unity.Scenes.SubScene>();
                if (ss != null && ss.SceneAsset != null)
                    return AssetDatabase.GetAssetPath(ss.SceneAsset);
            }
            return null;
        }
    }
}
#endif
