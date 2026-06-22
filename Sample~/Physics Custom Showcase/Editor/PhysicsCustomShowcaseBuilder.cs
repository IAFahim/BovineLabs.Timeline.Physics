#if UNITY_EDITOR
using System.Text;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Material = Unity.Physics.Material;

namespace BovineLabs.Timeline.Physics.Sample.PhysicsCustom
{

    public static class PhysicsCustomShowcaseBuilder
    {
        private const string Root = "PhysicsCustomShowcase";

        [MenuItem("Tools/BovineLabs/Samples/Build Physics Custom Showcase")]
        public static void BuildMenu() => Debug.Log(Build());

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

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "BLOCKED|play mode";

            var parent = EditorSceneManager.GetActiveScene();
            var parentPath = parent.path;
            var subPath = FindSubScenePath(parent);
            if (string.IsNullOrEmpty(subPath))
                return "BLOCKED|No SubScene in the active scene — open a scene containing a Unity.Scenes.SubScene first.";
            var sub = EditorSceneManager.OpenScene(subPath, OpenSceneMode.Additive);
            var log = new StringBuilder();
            try
            {
                EditorSceneManager.SetActiveScene(sub);

                foreach (var go in sub.GetRootGameObjects())
                    if (go.name == Root)
                        Object.DestroyImmediate(go);

                var root = new GameObject(Root);
                EditorSceneManager.MoveGameObjectToScene(root, sub);

                var ground = Box("Ground (Static)", new Vector3(0, -0.5f, -16), new Vector3(40, 1, 22), root);
                Shape(ground);

                var sx = new[] { -10f, -5f, 0f, 5f };
                BoxShape(Dynamic(Box("Shape: Box", new Vector3(sx[0], 4, -9), Vector3.one, root)));
                SphereShape(Dynamic(Sphere("Shape: Sphere", new Vector3(sx[1], 4, -9), root)));
                CylinderShape(Dynamic(Cyl("Shape: Cylinder", new Vector3(sx[2], 4, -9), root)));
                ConvexShape(Dynamic(Sphere("Shape: ConvexHull", new Vector3(sx[3], 4, -9), root)));
                log.AppendLine("SHAPES|Box,Sphere,Cylinder,ConvexHull (Capsule/Mesh/Plane available, not instanced)");

                BoxShape(Body(Box("Motion: Dynamic", new Vector3(-6, 4, -13), Vector3.one, root), BodyMotionType.Dynamic).gameObject);
                BoxShape(Body(Box("Motion: Kinematic", new Vector3(0, 1.5f, -13), new Vector3(3, 0.4f, 3), root), BodyMotionType.Kinematic).gameObject);
                BoxShape(Body(Box("Motion: Static", new Vector3(6, 1.5f, -13), new Vector3(1, 3, 1), root), BodyMotionType.Static).gameObject);
                log.AppendLine("MOTION|Dynamic,Kinematic,Static");

                var rest = new[] { 0.1f, 0.6f, 0.95f };
                for (var i = 0; i < 3; i++)
                {
                    var s = SphereShape(Dynamic(Sphere($"Restitution {rest[i]:0.00}", new Vector3(-6 + i * 3, 6, -17), root)));
                    s.OverrideRestitution = true;
                    s.Restitution = new PhysicsMaterialCoefficient { Value = rest[i], CombineMode = Material.CombinePolicy.Maximum };
                }
                var ramp = Box("Friction Ramp (Static)", new Vector3(7, 1.2f, -17), new Vector3(5, 0.3f, 4), root);
                ramp.transform.rotation = Quaternion.Euler(0, 0, 22);
                Shape(ramp);
                var slider = BoxShape(Dynamic(Box("Low-friction Slider", new Vector3(7, 3.0f, -17), new Vector3(0.8f, 0.8f, 0.8f), root)));
                slider.OverrideFriction = true;
                slider.Friction = new PhysicsMaterialCoefficient { Value = 0.02f, CombineMode = Material.CombinePolicy.Minimum };
                log.AppendLine("MATERIALS|restitution 0.1/0.6/0.95 + low-friction ramp slider");

                var trigger = Box("Trigger Zone (RaiseTriggerEvents)", new Vector3(-6, 1.5f, -21), new Vector3(3, 3, 3), root);
                var ts = Shape(trigger);
                ts.OverrideCollisionResponse = true;
                ts.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
                SphereShape(Dynamic(Sphere("Falls Through Trigger", new Vector3(-6, 6, -21), root)));

                var ce = SphereShape(Dynamic(Sphere("Raises Collision Events", new Vector3(0, 5, -21), root)));
                ce.OverrideCollisionResponse = true;
                ce.CollisionResponse = CollisionResponsePolicy.CollideRaiseCollisionEvents;
                log.AppendLine("RESPONSE|RaiseTriggerEvents zone + CollideRaiseCollisionEvents body");

                var pivot = new Vector3(-10, 6, -25);
                var pend = SphereShape(Dynamic(Sphere("Joint: Pendulum", pivot + new Vector3(2.5f, -1.5f, 0), root)));
                var bs = pend.gameObject.AddComponent<BallAndSocketJoint>();
                bs.ConnectedBody = null;
                bs.AutoSetConnected = false;
                bs.PositionLocal = float3.zero;
                bs.PositionInConnectedEntity = pivot;

                var frame = Box("Joint: Door Frame (Static)", new Vector3(-2, 2.5f, -25), new Vector3(0.3f, 3, 0.3f), root);
                Body(frame, BodyMotionType.Static); BoxShape(frame);

                var door = BoxShape(Dynamic(Box("Joint: Door", new Vector3(-0.8f, 2.5f, -25), new Vector3(2.4f, 2.6f, 0.15f), root)));
                var hinge = door.gameObject.AddComponent<LimitedHingeJoint>();
                hinge.ConnectedBody = frame.GetComponent<PhysicsBodyAuthoring>();
                hinge.AutoSetConnected = true;
                hinge.PositionLocal = new float3(-1.2f, 0f, 0f);
                hinge.HingeAxisLocal = new float3(0, 1, 0);
                hinge.PerpendicularAxisLocal = new float3(1, 0, 0);
                hinge.MinAngle = -1.6f;
                hinge.MaxAngle = 1.6f;

                var anchor = Box("Joint: Chain Anchor (Static)", new Vector3(8, 6, -25), new Vector3(0.4f, 0.4f, 0.4f), root);
                Body(anchor, BodyMotionType.Static); BoxShape(anchor);
                PhysicsBodyAuthoring prev = anchor.GetComponent<PhysicsBodyAuthoring>();
                for (var i = 0; i < 3; i++)
                {

                    var link = SphereShape(Dynamic(Sphere($"Joint: Chain Link {i + 1}", new Vector3(8, 5.3f - i * 1.0f, -25), root)));
                    var dist = link.gameObject.AddComponent<LimitedDistanceJoint>();
                    dist.ConnectedBody = prev;
                    dist.AutoSetConnected = true;
                    dist.PositionLocal = new float3(0f, 0.5f, 0f);
                    dist.MinDistance = 0f;
                    dist.MaxDistance = 0.12f;
                    prev = link.GetComponent<PhysicsBodyAuthoring>();
                }
                log.AppendLine("JOINTS|BallAndSocket pendulum, LimitedHinge door, LimitedDistance chain");

                EditorSceneManager.MarkSceneDirty(sub);
                EditorSceneManager.SaveScene(sub);
                var count = 0;
                foreach (Transform t in root.transform) count++;
                log.AppendLine("SAVED|" + count + " showcase objects");
            }
            catch (System.Exception e)
            {
                log.AppendLine("EXCEPTION|" + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                EditorSceneManager.SetActiveScene(parent);
                EditorSceneManager.CloseScene(sub, false);
                EditorSceneManager.OpenScene(parentPath, OpenSceneMode.Single);
            }
            return log.ToString();
        }

        private static GameObject Strip(GameObject go)
        {
            var c = go.GetComponent<UnityEngine.Collider>();
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }

        private static GameObject Box(string name, Vector3 pos, Vector3 size, GameObject parent)
        {
            var go = Strip(GameObject.CreatePrimitive(PrimitiveType.Cube));
            go.name = name; go.transform.SetParent(parent.transform, true);
            go.transform.position = pos; go.transform.localScale = size;
            return go;
        }

        private static GameObject Sphere(string name, Vector3 pos, GameObject parent)
        {
            var go = Strip(GameObject.CreatePrimitive(PrimitiveType.Sphere));
            go.name = name; go.transform.SetParent(parent.transform, true);
            go.transform.position = pos;
            return go;
        }

        private static GameObject Cyl(string name, Vector3 pos, GameObject parent)
        {
            var go = Strip(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            go.name = name; go.transform.SetParent(parent.transform, true);
            go.transform.position = pos;
            return go;
        }

        private static PhysicsBodyAuthoring Body(GameObject go, BodyMotionType mt)
        {
            var b = go.AddComponent<PhysicsBodyAuthoring>();
            b.MotionType = mt; b.Mass = 1f; b.LinearDamping = 0.05f;
            return b;
        }

        private static GameObject Dynamic(GameObject go) { Body(go, BodyMotionType.Dynamic); return go; }

        private static PhysicsShapeAuthoring Shape(GameObject go) => go.AddComponent<PhysicsShapeAuthoring>();

        private static PhysicsShapeAuthoring BoxShape(GameObject go)
        {
            var s = Shape(go);
            s.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1, 1, 1), Orientation = quaternion.identity, BevelRadius = 0.05f });
            return s;
        }

        private static PhysicsShapeAuthoring SphereShape(GameObject go)
        {
            var s = Shape(go);
            s.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity);
            return s;
        }

        private static PhysicsShapeAuthoring CylinderShape(GameObject go)
        {
            var s = Shape(go);
            s.SetCylinder(new CylinderGeometry { Center = float3.zero, Height = 2f, Radius = 0.5f, Orientation = quaternion.identity, SideCount = 20, BevelRadius = 0.05f });
            return s;
        }

        private static PhysicsShapeAuthoring ConvexShape(GameObject go)
        {
            var s = Shape(go);
            s.SetConvexHull(ConvexHullGenerationParameters.Default, go.GetComponent<MeshFilter>().sharedMesh);
            return s;
        }
    }
}
#endif
