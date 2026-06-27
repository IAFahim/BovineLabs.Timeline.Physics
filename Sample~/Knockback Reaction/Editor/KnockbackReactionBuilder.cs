using System;
using System.Reflection;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using BovineLabs.Core.Authoring.LifeCycle;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Authoring.Core;
using EventWriterAuthoring = BovineLabs.Reaction.Authoring.Conditions.EventWriterAuthoring;
using BovineLabs.Reaction.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Authoring;
using Material = UnityEngine.Material;
using Collider = UnityEngine.Collider;
using Target = BovineLabs.Reaction.Data.Core.Target;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TriggerAuthoring = BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring;
using EventState = BovineLabs.Core.PhysicsStates.StatefulEventState;
using ForceMode = BovineLabs.Timeline.Physics.PhysicsForceMode;
using ForceDir = BovineLabs.Timeline.Physics.PhysicsForceDirectionMode;

/// <summary>
/// DATA-DRIVEN directional knockback (front +Z vertical slice).
/// Chain: trigger circle (StatefulTriggerEvent) -> PhysicsTriggerConditionClip fires the
/// OnKnockFront ConditionEvent routed to the player -> player's ReactionAuthoring listens
/// for OnKnockFront -> the reaction plays a PhysicsForceClip timeline (via ActionTimeline)
/// that impulses the player backward.
/// Separate from the custom-system Assets/Samples/KnockbackRing scene.
/// </summary>
public static class KnockbackReactionBuilder
{
    private const string SampleFolder = "Assets/Samples/KnockbackRingReaction";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/KnockbackRingReaction.unity";
    private const string SubPath = SampleFolder + "/KnockbackRingReaction_Sub.unity";

    private const string EventFolder = "Assets/Settings/Schemas/Events";
    private const string RequiredInSubscenePrefab = "Assets/Prefabs/Required In Subscene.prefab";

    // categories
    private const uint CatGround = 1u << 0;
    private const uint CatBody = 1u << 1;
    private const uint CatTrigger = 1u << 3;

    private const int Directions = 8;          // full 45-degree ring
    private const float Radius = 1.2f;          // circle distance from player centre, XZ plane
    private const float SphereRadius = 0.5f;    // 2r=1.0 > chord 0.918 => overlap, no angular gap
    private const float KnockStrength = 6f;     // horizontal knock magnitude (away from circle)
    private const float KnockLift = 4f;         // vertical lift

    // 3/4 overhead view that frames the whole ring + knockback travel.
    private static readonly Vector3 CameraPos = new Vector3(7f, 6f, -10f);
    private static readonly Vector3 CameraEuler = new Vector3(24f, -24f, 0f);
    private const float CameraFov = 55f;

    private static Scene activeSub;

    [MenuItem("Knockback/Build Reaction Ring")]
    public static void Build()
    {
        EnsureFolders();

        // 1) Ensure the 8 ConditionEvent assets exist and each has a non-zero Key (force AutoRef).
        var events = new ConditionEventObject[Directions];
        for (var i = 0; i < Directions; i++)
        {
            events[i] = EnsureEventAsset(i);
            if (events[i] == null || events[i].Key == 0)
            {
                Debug.LogError($"KnockbackReaction: OnKnock_{i} event Key is still 0 (key={events[i]?.Key}); aborting before scene build.");
                return;
            }
        }

        Debug.Log($"KnockbackReaction: 8 events ready (keys {events[0].Key}..{events[Directions - 1].Key})");

        // 2) Build the scenes.
        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        InstantiateRequiredInSubscene();
        BuildFloor();

        var player = BuildPlayer();

        // The single looping sensor timeline holds one trigger track per circle.
        var sensorTimeline = NewTimeline(TimelineFolder + "/Sensor_Ring.playable");
        var sensorDir = MakeDirector("Ring_SensorDirector", playOnAwake: true);
        sensorDir.extrapolationMode = DirectorWrapMode.Loop;
        sensorDir.playableAsset = sensorTimeline;

        for (var i = 0; i < Directions; i++)
        {
            BuildDirection(player, events[i], i, sensorTimeline, sensorDir);
        }

        FixDuration(sensorTimeline);
        EditorUtility.SetDirty(sensorTimeline);
        foreach (var tr in sensorTimeline.GetOutputTracks())
        {
            EditorUtility.SetDirty(tr);
        }

        EditorUtility.SetDirty(sensorDir);
        AssetDatabase.SaveAssets();

        // Drop balls over front (+Z, dir 0) and east (+X, dir 2) so two directions can be seen.
        BuildDropBall("DropBall_Front", DirVector(0) * Radius);
        BuildDropBall("DropBall_East", DirVector(2) * Radius);

        // The Preload auto-setup drives the rendering Main Camera with a CinemachineBrain that follows a
        // CinemachineCamera vcam (injected into this sub). Frame the vcam so the brain frames the whole ring.
        FrameCinemachine(sub);

        EditorSceneManager.SaveScene(sub, SubPath);
        EditorSceneManager.SetActiveScene(parent);
        EditorSceneManager.CloseScene(sub, true);

        EditorSceneManager.SetActiveScene(parent);
        BuildParent();
        EditorSceneManager.SaveScene(parent);

        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("KnockbackReaction: built FULL 8-direction ring at " + ParentPath);
    }

    // Outward unit direction for circle i (XZ plane, 45-degree steps).
    private static Vector3 DirVector(int i)
    {
        var angle = i * (2f * Mathf.PI / Directions);
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }

    // ============================================================
    //  ConditionEvent asset + AutoRef key forcing
    // ============================================================

    private static ConditionEventObject EnsureEventAsset(int i)
    {
        var path = $"{EventFolder}/OnKnock_{i}.asset";
        var ev = AssetDatabase.LoadAssetAtPath<ConditionEventObject>(path);
        if (ev == null)
        {
            ev = ScriptableObject.CreateInstance<ConditionEventObject>();
            AssetDatabase.CreateAsset(ev, path);
            AssetDatabase.SaveAssets();
        }

        if (ev.Key == 0)
        {
            ForceObjectManagementProcessor();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ev = AssetDatabase.LoadAssetAtPath<ConditionEventObject>(path);
        }

        return ev;
    }

    private static void ForceObjectManagementProcessor()
    {
        Type procType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            procType = asm.GetType("BovineLabs.Core.Editor.ObjectManagement.ObjectManagementProcessor");
            if (procType != null)
            {
                break;
            }
        }

        if (procType == null)
        {
            Debug.LogError("KnockbackReaction: ObjectManagementProcessor type not found; cannot force AutoRef key.");
            return;
        }

        var method = procType.GetMethod("DelayedExecution", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            Debug.LogError("KnockbackReaction: ObjectManagementProcessor.DelayedExecution not found.");
            return;
        }

        method.Invoke(null, null);
    }

    // ============================================================
    //  Player (dynamic body + reaction listener + force timeline)
    // ============================================================

    private static GameObject BuildPlayer()
    {
        // Visible body + collider (rests on floor).
        var player = MakePrimitive(PrimitiveType.Cube, "Player", new Vector3(0f, 1.0f, 0f), new Vector3(1f, 2f, 1f), new Color(0.3f, 0.6f, 1f));

        var shape = player.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = Tags(CatBody);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = Tags(CatGround);
        shape.OverrideFriction = true;
        shape.Friction = new PhysicsMaterialCoefficient { Value = 0.4f, CombineMode = Unity.Physics.Material.CombinePolicy.GeometricMean };

        var body = player.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Dynamic;
        body.Mass = 1f;
        body.GravityFactor = 1f;
        body.LinearDamping = 0.05f;
        body.AngularDamping = 20f; // high angular damping so it doesn't tip
        EditorUtility.SetDirty(body);

        // Reaction listener requires LifeCycle + Targets (auto-added by RequireComponent, but be explicit).
        if (player.GetComponent<LifeCycleAuthoring>() == null)
        {
            player.AddComponent<LifeCycleAuthoring>();
        }

        var targets = player.GetComponent<TargetsAuthoring>();
        if (targets == null)
        {
            targets = player.AddComponent<TargetsAuthoring>();
        }

        targets.Owner = player;
        targets.Source = player;
        targets.Target = player;
        EditorUtility.SetDirty(targets);

        // The player must be able to RECEIVE routed condition events: EventWriterAuthoring adds the
        // ConditionEvent buffer + EventSubscriber + EventsDirty that ConditionEventWriteSystem requires.
        // Without it the routed OnKnock_i events land nowhere and the reactions never activate.
        if (player.GetComponent<EventWriterAuthoring>() == null)
        {
            player.AddComponent<EventWriterAuthoring>();
        }

        return player;
    }

    // ============================================================
    //  One ring direction: circle (sensor) + reaction + action + force timeline
    // ============================================================

    private static void BuildDirection(GameObject player, ConditionEventObject ev, int i, TimelineAsset sensorTimeline, PlayableDirector sensorDir)
    {
        var outward = DirVector(i); // unit direction the circle sits from the player

        // --- trigger circle: CHILD of player so it follows; sits at radius along outward dir ---
        var circle = MakePrimitive(PrimitiveType.Sphere, $"Circle_{i}", Vector3.zero, Vector3.one, RingColor(i));
        circle.transform.SetParent(player.transform, false);
        circle.transform.localPosition = outward * Radius;
        circle.transform.localScale = Vector3.one * (SphereRadius * 2f); // primitive sphere is diameter 1

        var shape = circle.AddComponent<PhysicsShapeAuthoring>();
        shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity); // local radius (scaled by 2r)
        shape.OverrideCollisionResponse = true;
        shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = Tags(CatTrigger);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = Tags(CatBody);

        var body = circle.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Kinematic;
        body.Mass = 1f;
        body.GravityFactor = 0f;

        circle.AddComponent<TriggerAuthoring>();

        // ReactionAuthoring + ActionTimelineAuthoring are [DisallowMultipleComponent], so each direction
        // gets its OWN reaction-carrier child (Knock_i). The routed event must land on the reaction's own
        // entity, so the circle routes the OnKnock_i event to this child (circle.Owner = knock), while the
        // force is applied to the player body (knock.Owner = player; action binding Target.Owner; ignoreTarget
        // = circle.Source = player body so the player's own collider doesn't trip the trigger).
        var knock = new GameObject($"Knock_{i}");
        SceneManager.MoveGameObjectToScene(knock, activeSub);
        knock.transform.SetParent(player.transform, false);

        if (knock.GetComponent<LifeCycleAuthoring>() == null)
        {
            knock.AddComponent<LifeCycleAuthoring>();
        }

        var knockTargets = knock.GetComponent<TargetsAuthoring>() ?? knock.AddComponent<TargetsAuthoring>();
        knockTargets.Owner = player; // force routes here -> the player body is knocked
        knockTargets.Source = player;
        knockTargets.Target = player;
        EditorUtility.SetDirty(knockTargets);

        // The reaction entity must own the ConditionEvent buffer (EventSubscriber pipeline) to receive the route.
        if (knock.GetComponent<EventWriterAuthoring>() == null)
        {
            knock.AddComponent<EventWriterAuthoring>();
        }

        // Circle Targets: route the event to the Knock_i child (Owner); ignore the player body (Source).
        var targets = circle.AddComponent<TargetsAuthoring>();
        targets.Owner = knock;     // routeTo=Owner -> reaction entity receives OnKnock_i
        targets.Source = player;   // ignoreTarget=Source -> player's own collider ignored
        targets.Target = knock;
        EditorUtility.SetDirty(targets);

        // --- sensor track for this circle on the shared looping timeline ---
        var triggerTrack = sensorTimeline.CreateTrack<StatefulTriggerTrack>(null, $"Trigger_{i}");
        var cc = AddClip<PhysicsTriggerConditionClip>(triggerTrack, 0.0, 4.0, $"contact -> OnKnock_{i}");
        var ca = (PhysicsTriggerConditionClip)cc.asset;
        // Enter: the ball drops from above and the overlap begins ~1s in (not frame 0), so Enter fires
        // cleanly and gives one knock per touch (no first-frame init race like the single-slice had).
        ca.triggerState = EventState.Enter;
        ca.collidesWith = Tags(CatBody);
        ca.condition = ev;
        ca.value = 1;
        ca.routeTo = Target.Owner;     // -> circle.Owner = Knock_i child (reaction entity)
        ca.ignoreTarget = Target.Source; // -> circle.Source = player body
        EditorUtility.SetDirty(cc.asset);
        sensorDir.SetGenericBinding(triggerTrack, circle.GetComponent<TriggerAuthoring>());

        // --- force timeline: knock AWAY from this circle (-outward), with lift ---
        var forceTimeline = NewTimeline(TimelineFolder + $"/Force_{i}.playable");
        var forceTrack = forceTimeline.CreateTrack<PhysicsForceTrack>(null, "Force");
        var fc = AddClip<PhysicsForceClip>(forceTrack, 0.0, 0.3, "knock away");
        var fa = (PhysicsForceClip)fc.asset;
        fa.mode = ForceMode.Impulse;
        fa.directionMode = ForceDir.FixedVector;
        fa.linearForce = new Vector3(-outward.x * KnockStrength, KnockLift, -outward.z * KnockStrength); // WORLD
        fa.space = Target.None;
        EditorUtility.SetDirty(fc.asset);
        FixDuration(forceTimeline);
        EditorUtility.SetDirty(forceTimeline);
        foreach (var tr in forceTimeline.GetOutputTracks())
        {
            EditorUtility.SetDirty(tr);
        }

        AssetDatabase.SaveAssets();

        var forceDir = MakeDirector($"Player_ForceDirector_{i}", playOnAwake: false);
        forceDir.playableAsset = forceTimeline;
        forceDir.SetGenericBinding(forceTrack, player.GetComponent<PhysicsBodyAuthoring>());
        EditorUtility.SetDirty(forceDir);

        // --- reaction listening for OnKnock_i (on the Knock_i child entity) ---
        var reaction = knock.AddComponent<ReactionAuthoring>();
        ConfigureKnockReaction(reaction, ev);

        // --- action timeline plays the force when the reaction is active; force routes to knock.Owner = player ---
        var actionTimeline = knock.AddComponent<ActionTimelineAuthoring>();
        actionTimeline.Timelines = new[]
        {
            new ActionTimelineAuthoring.Data
            {
                Director = forceDir,
                DisableTimelineOnDeactivate = true,
                ResetWhenActive = true,
                Bindings = new[]
                {
                    new ActionTimelineAuthoring.Data.Binding
                    {
                        Track = forceTrack,
                        Target = Target.Owner, // knock.Owner = player body
                    },
                },
            },
        };
        EditorUtility.SetDirty(actionTimeline);
    }

    private static Color RingColor(int i)
    {
        return Color.HSVToRGB(i / (float)Directions, 0.7f, 0.95f);
    }

    private static void ConfigureKnockReaction(ReactionAuthoring reaction, ConditionEventObject ev)
    {
        var so = new SerializedObject(reaction);

        so.FindProperty("Active.trigger").boolValue = false;
        so.FindProperty("Active.cooldown").floatValue = 0f;
        so.FindProperty("Active.cooldownAfterDuration").boolValue = false;
        so.FindProperty("Active.duration").floatValue = 2.0f;
        so.FindProperty("Active.cancellable").boolValue = false;

        var chance = so.FindProperty("Conditions.chanceToTrigger");
        chance.floatValue = 1f;

        var conditions = so.FindProperty("Conditions.conditions");
        conditions.ClearArray();
        conditions.InsertArrayElementAtIndex(0);
        var row = conditions.GetArrayElementAtIndex(0);
        row.FindPropertyRelative("Condition").objectReferenceValue = ev;
        row.FindPropertyRelative("Target").enumValueIndex = EnumIndexOfSelf(); // Self
        row.FindPropertyRelative("Operation").enumValueIndex = 0;              // Any
        row.FindPropertyRelative("Features").intValue = 1;                     // ConditionFeature.Condition -> gates Active
        row.FindPropertyRelative("DestroyIfTargetDestroyed").boolValue = false;
        row.FindPropertyRelative("CancelActive").boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(reaction);
    }

    // Target enum: None=0,Target=1,Owner=2,Source=3,Self=4(index 4),Custom=6(index 5).
    private static int EnumIndexOfSelf()
    {
        return 4;
    }

    // ============================================================
    //  Drop ball (falls onto a circle, activating the trigger)
    // ============================================================

    private static void BuildDropBall(string name, Vector3 xzOffset)
    {
        var ball = MakePrimitive(PrimitiveType.Sphere, name, new Vector3(xzOffset.x, 8.0f, xzOffset.z), new Vector3(0.6f, 0.6f, 0.6f), new Color(0.95f, 0.3f, 0.25f));

        var shape = ball.AddComponent<PhysicsShapeAuthoring>();
        shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity);
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = Tags(CatBody);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = Tags(CatGround | CatTrigger);

        var body = ball.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Dynamic;
        body.Mass = 1f;
        body.GravityFactor = 1f;
        body.LinearDamping = 0.05f;
        body.AngularDamping = 0.05f;
        EditorUtility.SetDirty(body);
    }

    // ============================================================
    //  Floor + scene chrome
    // ============================================================

    private static void BuildFloor()
    {
        var floor = MakePrimitive(PrimitiveType.Cube, "Floor", new Vector3(0f, -0.4f, 0f), new Vector3(20f, 0.8f, 20f), new Color(0.2f, 0.22f, 0.27f));
        var shape = floor.AddComponent<PhysicsShapeAuthoring>();
        shape.SetBox(new BoxGeometry { Center = float3.zero, Size = new float3(1f, 1f, 1f), Orientation = quaternion.identity, BevelRadius = 0.02f });
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = Tags(CatGround);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = Tags(CatBody);

        var body = floor.AddComponent<PhysicsBodyAuthoring>();
        body.MotionType = BodyMotionType.Static;
    }

    private static void InstantiateRequiredInSubscene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RequiredInSubscenePrefab);
        if (prefab == null)
        {
            Debug.LogError("KnockbackReaction: Required In Subscene prefab missing at " + RequiredInSubscenePrefab);
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeSub);
        go.name = "Required In Subscene";
        EditorUtility.SetDirty(go);
    }

    // Frame the Cinemachine vcam (and any plain camera) in a scene so the brain frames the whole ring.
    // Uses reflection so this Editor script needs no hard Cinemachine assembly reference.
    private static void FrameCinemachine(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                {
                    continue;
                }

                if (mb.GetType().Name == "CinemachineCamera")
                {
                    var t = mb.transform;
                    t.position = CameraPos;
                    t.rotation = Quaternion.Euler(CameraEuler.x, CameraEuler.y, CameraEuler.z);

                    var so = new SerializedObject(mb);
                    var fov = so.FindProperty("Lens.FieldOfView");
                    if (fov != null)
                    {
                        fov.floatValue = CameraFov;
                    }

                    var far = so.FindProperty("Lens.FarClipPlane");
                    if (far != null)
                    {
                        far.floatValue = 200f;
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                    EditorUtility.SetDirty(t);
                }
            }

            // Plain-camera fallback (no Cinemachine brain present).
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            {
                var t = cam.transform;
                t.position = CameraPos;
                t.rotation = Quaternion.Euler(CameraEuler.x, CameraEuler.y, CameraEuler.z);
                cam.fieldOfView = CameraFov;
                cam.farClipPlane = 200f;
                EditorUtility.SetDirty(cam);
                EditorUtility.SetDirty(t);
            }
        }
    }

    private static void BuildParent()
    {
        RenderSettings.fog = false;

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        if (sceneAsset == null)
        {
            Debug.LogError("KnockbackReaction: sub-scene asset missing at " + SubPath);
            return;
        }

        var subSceneGo = new GameObject("Knockback SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);

        // The Preload auto-setup also injects a "Required In Scene" rig into the PARENT scene; frame it too.
        FrameCinemachine(EditorSceneManager.GetActiveScene());
    }

    // ============================================================
    //  helpers
    // ============================================================

    private static GameObject MakePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        return go;
    }

    private static PlayableDirector MakeDirector(string name, bool playOnAwake)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, activeSub);
        var director = go.AddComponent<PlayableDirector>();
        director.playOnAwake = playOnAwake;
        return director;
    }

    private static TimelineAsset NewTimeline(string path)
    {
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, path);
        return timeline;
    }

    private static TimelineClip AddClip<T>(TrackAsset track, double start, double duration, string name) where T : PlayableAsset
    {
        var clip = track.CreateClip<T>();
        clip.start = start;
        clip.duration = duration;
        clip.displayName = name;
        return clip;
    }

    private static void FixDuration(TimelineAsset timeline)
    {
        var end = 0.0;
        foreach (var track in timeline.GetOutputTracks())
        {
            foreach (var clip in track.GetClips())
            {
                var clipEnd = clip.start + clip.duration;
                if (clipEnd > end)
                {
                    end = clipEnd;
                }
            }
        }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
    }

    private static PhysicsCategoryTags Tags(uint value)
    {
        return new PhysicsCategoryTags { Value = value };
    }

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        var mat = new Material(shader) { name = name + "_Mat" };
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

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
            AssetDatabase.CreateFolder("Assets/Samples", "KnockbackRingReaction");
        }

        if (!AssetDatabase.IsValidFolder(TimelineFolder))
        {
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Settings/Schemas/Events"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Schemas"))
            {
                AssetDatabase.CreateFolder("Assets/Settings", "Schemas");
            }

            AssetDatabase.CreateFolder("Assets/Settings/Schemas", "Events");
        }
    }
}
