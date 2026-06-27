namespace Vex.Knockback.Editor
{
    using Unity.Mathematics;
    using Unity.Physics;
    using Unity.Physics.Authoring;
    using Unity.Scenes;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Material = UnityEngine.Material;
    using Collider = UnityEngine.Collider;
    using TriggerAuthoring = BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring;

    /// <summary>
    /// Builds a self-contained showcase scene for directional knockback: a body ringed by eight overlapping
    /// stateful-trigger sphere zones (no angular gap), plus a ball you can drop from above to "activate" a zone and
    /// see the body leap away from the contact. Run via the menu or <c>unity-cli menu "Knockback/Build Ring"</c>.
    /// </summary>
    public static class KnockbackRingBuilder
    {
        private const string SampleFolder = "Assets/Samples/KnockbackRing";
        private const string MatFolder = SampleFolder + "/Materials";
        private const string ParentPath = SampleFolder + "/KnockbackRing.unity";
        private const string SubPath = SampleFolder + "/KnockbackRing_Sub.unity";

        // Collision categories.
        private const uint CatGround = 1u << 0;
        private const uint CatBody = 1u << 1;
        private const uint CatTrigger = 1u << 3;

        // Geometry.
        private const int ZoneCount = 8;
        private const float RingRadius = 1.2f;   // distance from body centre to each sphere centre
        private const float ZoneRadius = 0.5f;   // sphere radius — 2r (1.0) > adjacent chord (0.918) => overlap, no gap
        private const float BodyCentreY = 0.6f;  // body box half-height, so it rests on the floor at y=0

        private static Scene activeSub;
        private static Material zoneMat;

        [MenuItem("Knockback/Build Ring")]
        public static void Build()
        {
            EnsureFolders();
            ResetAssets();

            // One shared, translucent material for all eight zones — creating 8 assets at the same path would break
            // 7 of them (magenta). Translucent so the body is visible inside the ring.
            zoneMat = MakeTransparentMaterial("Zone", new Color(0.95f, 0.55f, 0.15f, 0.45f));

            var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(parent, ParentPath);

            var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(sub);
            activeSub = sub;

            BuildFloor();
            BuildPlayerWithZones();
            BuildDropBall();

            EditorSceneManager.SaveScene(sub, SubPath);
            EditorSceneManager.SetActiveScene(parent);
            EditorSceneManager.CloseScene(sub, true);

            BuildParent();
            EditorSceneManager.SaveScene(parent);
            EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log("KnockbackRing: built ring of " + ZoneCount + " trigger zones + drop ball at " + ParentPath);
        }

        // ---------------- CONTENT ----------------

        private static void BuildFloor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Floor";
            go.transform.position = new Vector3(0f, -0.4f, 0f);
            go.transform.localScale = new Vector3(40f, 0.8f, 40f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Floor", new Color(0.22f, 0.24f, 0.28f));

            var shape = go.AddComponent<PhysicsShapeAuthoring>();
            shape.SetBox(new BoxGeometry
            {
                Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity,
                BevelRadius = 0.02f,
            });
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = Tags(CatGround);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = Tags(CatBody);

            go.AddComponent<PhysicsBodyAuthoring>().MotionType = BodyMotionType.Static;

            SceneManager.MoveGameObjectToScene(go, activeSub);
        }

        private static void BuildPlayerWithZones()
        {
            // Root body: invisible box collider (solid), the dynamic body, the trigger-event buffer + knockback marker.
            var root = new GameObject("Player");
            root.transform.position = new Vector3(0f, BodyCentreY, 0f);

            var box = root.AddComponent<PhysicsShapeAuthoring>();
            box.SetBox(new BoxGeometry
            {
                Center = float3.zero, Size = new float3(0.6f, 1.2f, 0.6f), Orientation = quaternion.identity,
                BevelRadius = 0.02f,
            });
            box.OverrideFriction = true;
            box.Friction = new PhysicsMaterialCoefficient
            {
                Value = 0.6f, CombineMode = Unity.Physics.Material.CombinePolicy.GeometricMean,
            };
            box.OverrideBelongsTo = true;
            box.BelongsTo = Tags(CatBody);
            box.OverrideCollidesWith = true;
            box.CollidesWith = Tags(CatGround); // solid leaf: rest on the floor only; the ball passes through it

            var body = root.AddComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Dynamic;
            body.Mass = 1f;
            body.GravityFactor = 1f;
            body.LinearDamping = 0.2f;
            body.AngularDamping = 5f; // suppress tipping — the knockback is a pure linear (no-torque) impulse anyway

            root.AddComponent<TriggerAuthoring>();            // adds the StatefulTriggerEvent buffer
            var receiver = root.AddComponent<KnockbackReceiverAuthoring>();
            receiver.Strength = 5f;
            receiver.Lift = 4f;

            // Visible body mesh on an unscaled-root child so the ring children are not distorted.
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Body Mesh";
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
            mesh.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Player", new Color(0.30f, 0.55f, 0.95f));

            // Eight overlapping sphere trigger leaves ringed in the XZ plane. Each folds into the body's compound
            // collider (no PhysicsBodyAuthoring) so they follow the body, and each raises trigger events.
            for (var i = 0; i < ZoneCount; i++)
            {
                var angle = math.radians(i * (360f / ZoneCount));
                var dir = new Vector3(math.sin(angle), 0f, math.cos(angle));
                MakeZone(root.transform, ZoneLabel(i), dir * RingRadius);
            }

            SceneManager.MoveGameObjectToScene(root, activeSub);
        }

        private static void MakeZone(Transform parent, string label, Vector3 localPos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Zone_" + label;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * (ZoneRadius * 2f); // unit sphere mesh => diameter = 2r
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = zoneMat;

            var shape = go.AddComponent<PhysicsShapeAuthoring>();
            shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = ZoneRadius }, quaternion.identity);
            shape.OverrideCollisionResponse = true;
            shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = Tags(CatTrigger);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = Tags(CatBody);
        }

        private static void BuildDropBall()
        {
            // Dropped above the "front" zone (local +Z) so it falls through that trigger -> body leaps backward.
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DropBall";
            go.transform.position = new Vector3(0f, 6f, RingRadius);
            go.transform.localScale = Vector3.one * 0.7f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial("DropBall", new Color(0.9f, 0.2f, 0.25f));

            var shape = go.AddComponent<PhysicsShapeAuthoring>();
            shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.35f }, quaternion.identity);
            shape.OverrideRestitution = true;
            shape.Restitution = new PhysicsMaterialCoefficient
            {
                Value = 0.4f, CombineMode = Unity.Physics.Material.CombinePolicy.Maximum,
            };
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = Tags(CatBody);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = Tags(CatGround | CatTrigger); // lands on floor, activates zones, ignores the body box

            var body = go.AddComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Dynamic;
            body.Mass = 1f;
            body.GravityFactor = 1f;
            body.LinearDamping = 0.01f;
            body.AngularDamping = 0.05f;

            SceneManager.MoveGameObjectToScene(go, activeSub);
        }

        // ---------------- PARENT (camera / light / sub-scene link) ----------------

        private static void BuildParent()
        {
            RenderSettings.fog = false;

            // Reuse the camera the Preload system drops into "Required In Scene" instead of adding a duplicate.
            var required = GameObject.Find("Required In Scene");
            var camTransform = required != null ? required.transform.Find("Main Camera") : null;
            if (camTransform != null)
            {
                camTransform.position = new Vector3(7.5f, 6f, -10f);
                camTransform.rotation = Quaternion.Euler(24f, -24f, 0f);
                var cam = camTransform.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.fieldOfView = 55f;
                    cam.farClipPlane = 200f;
                    EditorUtility.SetDirty(cam);
                }

                EditorUtility.SetDirty(camTransform);
            }

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.9f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -28f, 0f);

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
            if (sceneAsset == null)
            {
                Debug.LogError("KnockbackRing: sub-scene asset missing at " + SubPath);
                return;
            }

            var subSceneGo = new GameObject("KnockbackRing SubScene");
            var subScene = subSceneGo.AddComponent<SubScene>();
            subScene.SceneAsset = sceneAsset;
            subScene.AutoLoadScene = true;
            EditorUtility.SetDirty(subScene);
        }

        // ---------------- HELPERS ----------------

        private static string ZoneLabel(int i)
        {
            // i*45deg clockwise from +Z (front).
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return i + "_" + names[i % names.Length];
        }

        private static PhysicsCategoryTags Tags(uint value)
        {
            return new PhysicsCategoryTags { Value = value };
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            var path = MatFolder + "/" + name + ".mat";
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material MakeTransparentMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };

            // URP Lit -> transparent surface (alpha blend, no depth write).
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, MatFolder + "/" + name + ".mat");
            return mat;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Samples"))
            {
                AssetDatabase.CreateFolder("Assets", "Samples");
            }

            if (!AssetDatabase.IsValidFolder(SampleFolder))
            {
                AssetDatabase.CreateFolder("Assets/Samples", "KnockbackRing");
            }

            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                AssetDatabase.CreateFolder(SampleFolder, "Materials");
            }
        }

        private static void ResetAssets()
        {
            if (AssetDatabase.IsValidFolder(MatFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MatFolder }))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            foreach (var p in new[] { ParentPath, SubPath })
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(p) != null)
                {
                    AssetDatabase.DeleteAsset(p);
                }
            }
        }
    }
}
