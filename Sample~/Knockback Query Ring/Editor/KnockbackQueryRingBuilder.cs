using BovineLabs.Core.Asset;
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
using BovineLabs.Nerve.Authoring.LifeCycle;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Authoring.Core;
using EventWriterAuthoring = BovineLabs.Reaction.Authoring.Conditions.EventWriterAuthoring;
using BovineLabs.Reaction.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Authoring;
using BovineLabs.Timeline.Physics;
using Material = UnityEngine.Material;
using Collider = UnityEngine.Collider;
using Target = BovineLabs.Reaction.Data.Core.Target;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TriggerAuthoring = BovineLabs.Nerve.Authoring.PhysicsStates.StatefulTriggerEventAuthoring;
using EventState = BovineLabs.Nerve.PhysicsStates.StatefulEventState;
using ForceMode = BovineLabs.Timeline.Physics.PhysicsForceMode;
using ForceDir = BovineLabs.Timeline.Physics.PhysicsForceDirectionMode;

/// <summary>
/// FLAGSHIP live proof of WAVE 1: 8-directional knockback off ONE following trigger shell + ONE PhysicsTriggerQuery.
///
/// Chain: a single bodyless sphere trigger shell (compound leaf parented to the player so it follows) raises
/// StatefulTriggerEvents -> ONE looping sensor timeline with ONE StatefulTriggerTrack bound to that shell holds
/// ONE PhysicsTriggerQueryClip (selection=Nearest, valueMode=DirectionSector, sectorCount=8, routeSlot=Custom).
/// On contact the query fires OnKnockSector(value = sector 0..7) at the player. The player carries 8 reaction
/// children, each Operation=Equal,Value=i; reaction i plays a per-direction force timeline that impulses the
/// player AWAY from sector i. One query + one shell launches each body along its true bearing.
/// </summary>
public static class KnockbackQueryRingBuilder
{
    private const string SampleFolder = "Assets/Samples/KnockbackQueryRing";
    private const string TimelineFolder = SampleFolder + "/Timelines";
    private const string ParentPath = SampleFolder + "/KnockbackQueryRing.unity";
    private const string SubPath = SampleFolder + "/KnockbackQueryRing_Sub.unity";

    private const string EventFolder = "Assets/Settings/Schemas/Events";
    private const string EventName = "OnKnockSector";
    private const string RequiredInSubscenePrefab = "Assets/Prefabs/Required In Subscene.prefab";

    // categories
    private const uint CatGround = 1u << 0;
    private const uint CatBody = 1u << 1;
    private const uint CatTrigger = 1u << 3;

    private const int Directions = 8;          // full 45-degree ring (sector count)
    private const float ShellRadius = 2.0f;    // the ONE sensor shell radius around the player (XZ reach)
    private const float KnockStrength = 6f;    // horizontal knock magnitude (away from sector)
    private const float KnockLift = 4f;        // vertical lift

    // 3/4 overhead view that frames the whole ring + knockback travel.
    private static readonly Vector3 CameraPos = new Vector3(8f, 7f, -11f);
    private static readonly Vector3 CameraEuler = new Vector3(26f, -26f, 0f);
    private const float CameraFov = 55f;

    private static Scene activeSub;

    [MenuItem("Knockback/Build Query Ring")]
    public static void Build()
    {
        EnsureFolders();

        // 1) Ensure the single ConditionEvent asset exists and has a non-zero Key (force AutoRef).
        var ev = EnsureEventAsset();
        if (ev == null || ev.Key == 0)
        {
            Debug.LogError($"KnockbackQueryRing: {EventName} event Key is still 0; aborting before scene build.");
            return;
        }

        Debug.Log($"KnockbackQueryRing: event ready (key {ev.Key})");

        // 2) Build scenes.
        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(sub);
        activeSub = sub;

        InstantiateRequiredInSubscene();
        BuildFloor();

        var player = BuildPlayer();
        BuildShell(player); // a bodyless trigger leaf ON the player -> follows it; trigger buffer lives on the player

        // The single looping sensor timeline: ONE trigger track, ONE query clip bound to the shell.
        var sensorTimeline = NewTimeline(TimelineFolder + "/Sensor_Query.playable");
        var sensorDir = MakeDirector("Query_SensorDirector", playOnAwake: true);
        sensorDir.extrapolationMode = DirectorWrapMode.Loop;
        sensorDir.playableAsset = sensorTimeline;

        BuildQueryTrack(sensorTimeline, sensorDir, player, ev);

        FixDuration(sensorTimeline);
        EditorUtility.SetDirty(sensorTimeline);
        foreach (var tr in sensorTimeline.GetOutputTracks())
            EditorUtility.SetDirty(tr);

        // 8 reaction children, each Operation=Equal,Value=i -> per-direction force timeline.
        for (var i = 0; i < Directions; i++)
            BuildDirectionReaction(player, ev, i);

        EditorUtility.SetDirty(sensorDir);
        AssetDatabase.SaveAssets();

        // Drop balls onto 3 different sides: front (+Z, sector 0), east (+X, sector 2), back (-Z, sector 4).
        // STAGGERED heights so each knock is temporally isolated within ONE play run (front first, then east,
        // then back) — lets the live before/after be read per side from one shell + one query.
        BuildDropBall("DropBall_Front", DirVector(0) * (ShellRadius * 0.6f), 8f);
        BuildDropBall("DropBall_East", DirVector(2) * (ShellRadius * 0.6f), 80f);
        BuildDropBall("DropBall_Back", DirVector(4) * (ShellRadius * 0.6f), 200f);

        FrameCinemachine(sub);

        EditorSceneManager.SaveScene(sub, SubPath);
        EditorSceneManager.SetActiveScene(parent);
        EditorSceneManager.CloseScene(sub, true);

        EditorSceneManager.SetActiveScene(parent);
        BuildParent();
        EditorSceneManager.SaveScene(parent);

        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("KnockbackQueryRing: built ONE-shell 8-direction query ring at " + ParentPath);
    }

    // Outward unit direction for sector i (XZ plane, 45-degree steps; sector 0 = +Z front).
    private static Vector3 DirVector(int i)
    {
        // Match the runtime sector basis: sector 0 = +Z, sector 2 = +X (right), measured clockwise in XZ.
        var angle = i * (2f * Mathf.PI / Directions);
        return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
    }

    // ============================================================
    //  ConditionEvent asset + AutoRef key forcing
    // ============================================================

    private static ConditionEventObject EnsureEventAsset()
    {
        var path = $"{EventFolder}/{EventName}.asset";
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
            procType = asm.GetType("BovineLabs.Nerve.Editor.ObjectManagement.ObjectManagementProcessor");
            if (procType != null)
                break;
        }

        if (procType == null)
        {
            Debug.LogError("KnockbackQueryRing: ObjectManagementProcessor type not found; cannot force AutoRef key.");
            return;
        }

        var method = procType.GetMethod("DelayedExecution", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null)
        {
            Debug.LogError("KnockbackQueryRing: ObjectManagementProcessor.DelayedExecution not found.");
            return;
        }

        method.Invoke(null, null);
    }

    // ============================================================
    //  Player (dynamic body + reaction listener)
    // ============================================================

    private static GameObject BuildPlayer()
    {
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

        if (player.GetComponent<LifeCycleAuthoring>() == null)
            player.AddComponent<LifeCycleAuthoring>();

        var targets = player.GetComponent<TargetsAuthoring>() ?? player.AddComponent<TargetsAuthoring>();
        targets.Owner = player;
        targets.Source = player;
        targets.Target = player;
        EditorUtility.SetDirty(targets);

        // The player must RECEIVE the routed OnKnockSector event: EventWriterAuthoring adds the ConditionEvent
        // buffer + EventSubscriber pipeline. Without it the routed event lands nowhere.
        if (player.GetComponent<EventWriterAuthoring>() == null)
            player.AddComponent<EventWriterAuthoring>();

        return player;
    }

    // ============================================================
    //  The ONE trigger shell (kinematic trigger body co-located with the player)
    // ============================================================

    private static void BuildShell(GameObject player)
    {
        // ONE sensor shell as a BODYLESS COMPOUND LEAF on the player: a sphere trigger child with NO
        // PhysicsBodyAuthoring folds into the player's dynamic body, so it FOLLOWS the player for free. A separate
        // body (kinematic or not) bakes as a WORLD-ROOT in DOTS — the parent transform is stripped — so it gets
        // left behind when the player is knocked (the "circle doesn't move with the player" bug). The custom-version
        // 8-sphere ring proves a trigger leaf on a dynamic body raises StatefulTriggerEvents reliably.
        var shell = MakePrimitive(PrimitiveType.Sphere, "QueryShell", player.transform.position, Vector3.one, new Color(1f, 1f, 1f, 0.25f));
        shell.transform.SetParent(player.transform, false);
        shell.transform.localPosition = Vector3.zero;
        shell.transform.localScale = Vector3.one * (ShellRadius * 2f); // primitive sphere is diameter 1

        var shape = shell.AddComponent<PhysicsShapeAuthoring>();
        shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity); // local; scaled by 2r
        shape.OverrideCollisionResponse = true;
        shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
        shape.OverrideBelongsTo = true;
        shape.BelongsTo = Tags(CatTrigger);
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = Tags(CatBody);
        // NO PhysicsBodyAuthoring here -> the trigger sphere folds into the player's compound and follows it.

        // The StatefulTriggerEvent buffer lives on the BODY entity (the player); the StatefulTriggerTrack binds to
        // the player's TriggerAuthoring, so the query's self resolves to the player. The player already carries
        // Targets (Owner/Source/Target = player, set in BuildPlayer), so routeTo=Owner / ignoreTarget=Source still
        // resolve to the player exactly as before.
        if (player.GetComponent<TriggerAuthoring>() == null)
            player.AddComponent<TriggerAuthoring>();
    }

    // ============================================================
    //  The ONE query track + clip (the flagship)
    // ============================================================

    private static void BuildQueryTrack(TimelineAsset sensorTimeline, PlayableDirector sensorDir,
        GameObject player, ConditionEventObject ev)
    {
        var track = sensorTimeline.CreateTrack<StatefulTriggerTrack>(null, "Query");
        var clip = AddClip<PhysicsTriggerQueryClip>(track, 0.0, 4.0, "DirectionSector -> OnKnockSector");
        var q = (PhysicsTriggerQueryClip)clip.asset;

        q.triggerState = EventState.Enter;       // one knock per contact
        q.collidesWith = Tags(CatBody);
        q.selection = PhysicsTriggerQuerySelection.Nearest;

        // FLAGSHIP value mode.
        q.valueMode = PhysicsTriggerQueryValueMode.DirectionSector;
        q.sectorCount = 8;
        q.sectorReference = PhysicsTriggerSectorReference.SelfForward;
        q.sectorPlane = PhysicsTriggerSectorPlane.XZ;
        q.sectorHysteresis = -1f; // default ~0.15*binW

        q.routeTo = Target.Owner;                // player.Owner = player -> player receives OnKnockSector
        q.routeSlot = PhysicsTriggerRouteSlot.Custom;
        q.writeMode = PhysicsTriggerWriteMode.Set;
        q.ignoreTarget = Target.Source;          // player.Source = player -> the player ignores its own collider
        q.clearOnLost = false;

        q.foundCondition = ev;
        q.foundValue = 0; // overridden by the computed sector

        EditorUtility.SetDirty(clip.asset);
        // Bind to the PLAYER's trigger buffer (the shell is now a leaf of the player's compound); self -> player.
        sensorDir.SetGenericBinding(track, player.GetComponent<TriggerAuthoring>());
    }

    // ============================================================
    //  One direction's reaction child + force timeline
    // ============================================================

    private static void BuildDirectionReaction(GameObject player, ConditionEventObject ev, int i)
    {
        var outward = DirVector(i);

        var knock = new GameObject($"Knock_{i}");
        SceneManager.MoveGameObjectToScene(knock, activeSub);
        knock.transform.SetParent(player.transform, false);

        if (knock.GetComponent<LifeCycleAuthoring>() == null)
            knock.AddComponent<LifeCycleAuthoring>();

        var knockTargets = knock.GetComponent<TargetsAuthoring>() ?? knock.AddComponent<TargetsAuthoring>();
        knockTargets.Owner = player; // force routes here -> the player body is knocked
        knockTargets.Source = player;
        knockTargets.Target = player;
        EditorUtility.SetDirty(knockTargets);

        // The reaction entity must own the ConditionEvent buffer to receive the routed event.
        // The query routes to player(Owner); reactions live as children but read the player's event buffer via
        // the Self target on the condition row, so each child needs its own EventWriter to host its reaction.
        if (knock.GetComponent<EventWriterAuthoring>() == null)
            knock.AddComponent<EventWriterAuthoring>();

        // --- force timeline: knock AWAY from sector i (-outward), with lift ---
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
            EditorUtility.SetDirty(tr);

        AssetDatabase.SaveAssets();

        var forceDir = MakeDirector($"Player_ForceDirector_{i}", playOnAwake: false);
        forceDir.playableAsset = forceTimeline;
        forceDir.SetGenericBinding(forceTrack, player.GetComponent<PhysicsBodyAuthoring>());
        EditorUtility.SetDirty(forceDir);

        // --- reaction: fires when OnKnockSector value == i ---
        var reaction = knock.AddComponent<ReactionAuthoring>();
        ConfigureSectorReaction(reaction, ev, i);

        // --- action timeline plays the force; force routes to knock.Owner = player ---
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

    private static void ConfigureSectorReaction(ReactionAuthoring reaction, ConditionEventObject ev, int sector)
    {
        var so = new SerializedObject(reaction);

        so.FindProperty("Active.trigger").boolValue = false;
        so.FindProperty("Active.cooldown").floatValue = 0f;
        so.FindProperty("Active.cooldownAfterDuration").boolValue = false;
        so.FindProperty("Active.duration").floatValue = 2.0f;
        so.FindProperty("Active.cancellable").boolValue = false;

        so.FindProperty("Conditions.chanceToTrigger").floatValue = 1f;

        var conditions = so.FindProperty("Conditions.conditions");
        conditions.ClearArray();
        conditions.InsertArrayElementAtIndex(0);
        var row = conditions.GetArrayElementAtIndex(0);
        row.FindPropertyRelative("Condition").objectReferenceValue = ev;
        row.FindPropertyRelative("Target").enumValueIndex = EnumIndexOfOwner(); // Owner = player -> all 8 children
                                                                                // read the player's OnKnockSector buffer
        row.FindPropertyRelative("Operation").enumValueIndex = 1;               // Equality.Equal
        row.FindPropertyRelative("ComparisonMode").enumValueIndex = 0;          // Constant
        row.FindPropertyRelative("Value").intValue = sector;                    // fires only when sector == i
        row.FindPropertyRelative("Features").intValue = 1;                      // ConditionFeature.Condition -> gates Active
        row.FindPropertyRelative("DestroyIfTargetDestroyed").boolValue = false;
        row.FindPropertyRelative("CancelActive").boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(reaction);
    }

    // Target enum index: None=0,Target=1,Owner=2,Source=3,Self=4,Custom=5.
    private static int EnumIndexOfOwner() => 2;

    // ============================================================
    //  Drop ball
    // ============================================================

    private static void BuildDropBall(string name, Vector3 xzOffset, float dropHeight)
    {
        var ball = MakePrimitive(PrimitiveType.Sphere, name, new Vector3(xzOffset.x, dropHeight, xzOffset.z), new Vector3(0.6f, 0.6f, 0.6f), new Color(0.95f, 0.3f, 0.25f));

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
            Debug.LogError("KnockbackQueryRing: Required In Subscene prefab missing at " + RequiredInSubscenePrefab);
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, activeSub);
        go.name = "Required In Subscene";
        EditorUtility.SetDirty(go);
    }

    private static void FrameCinemachine(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;

                if (mb.GetType().Name == "CinemachineCamera")
                {
                    var t = mb.transform;
                    t.position = CameraPos;
                    t.rotation = Quaternion.Euler(CameraEuler.x, CameraEuler.y, CameraEuler.z);

                    var so = new SerializedObject(mb);
                    var fov = so.FindProperty("Lens.FieldOfView");
                    if (fov != null)
                        fov.floatValue = CameraFov;

                    var far = so.FindProperty("Lens.FarClipPlane");
                    if (far != null)
                        far.floatValue = 200f;

                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(mb);
                    EditorUtility.SetDirty(t);
                }
            }

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
            Debug.LogError("KnockbackQueryRing: sub-scene asset missing at " + SubPath);
            return;
        }

        var subSceneGo = new GameObject("Knockback SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);

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
        foreach (var clip in track.GetClips())
        {
            var clipEnd = clip.start + clip.duration;
            if (clipEnd > end)
                end = clipEnd;
        }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = end;
    }

    private static PhysicsCategoryTags Tags(uint value) => new PhysicsCategoryTags { Value = value };

    private static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name + "_Mat" };
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Samples"))
            AssetDatabase.CreateFolder("Assets", "Samples");

        if (!AssetDatabase.IsValidFolder(SampleFolder))
            AssetDatabase.CreateFolder("Assets/Samples", "KnockbackQueryRing");

        if (!AssetDatabase.IsValidFolder(TimelineFolder))
            AssetDatabase.CreateFolder(SampleFolder, "Timelines");

        if (!AssetDatabase.IsValidFolder("Assets/Settings/Schemas/Events"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Schemas"))
                AssetDatabase.CreateFolder("Assets/Settings", "Schemas");

            AssetDatabase.CreateFolder("Assets/Settings/Schemas", "Events");
        }
    }
}
