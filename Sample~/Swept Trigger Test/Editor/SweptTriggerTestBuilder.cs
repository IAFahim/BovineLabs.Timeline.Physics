using System.Collections.Generic;
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
using Material = UnityEngine.Material;
using Collider = UnityEngine.Collider;
using ObjectDefinition = BovineLabs.Nerve.Authoring.ObjectManagement.ObjectDefinition;
using ConditionEventObject = BovineLabs.Reaction.Authoring.Conditions.ConditionEventObject;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TargetSlot = BovineLabs.Reaction.Data.Core.Target;
using LifeCycleAuthoring = BovineLabs.Nerve.Authoring.LifeCycle.LifeCycleAuthoring;
using TimelineBeginAuthoring = BovineLabs.Timeline.Core.Authoring.TimelineBeginAuthoring;
using TimelineBeginMode = BovineLabs.Timeline.Core.Authoring.TimelineBeginMode;
using PositionTrack = BovineLabs.Timeline.Transform.Authoring.TransformPositionTrack;
using PositionClip = BovineLabs.Timeline.Transform.Authoring.PositionClip;
using PositionType = BovineLabs.Timeline.Transform.Authoring.PositionType;
using SweptTrack = BovineLabs.Timeline.Physics.Authoring.SweptTriggerTrack;
using SweptSource = BovineLabs.Timeline.Physics.Authoring.SweptTriggerSourceAuthoring;
using InstantiateClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerInstantiateClip;
using ConditionClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerConditionClip;
using ForceClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerForceClip;
using QueryClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerQueryClip;
using ForceType = BovineLabs.Timeline.Physics.PhysicsTriggerForceType;
using PhysForceMode = BovineLabs.Timeline.Physics.PhysicsForceMode;
using QuerySelection = BovineLabs.Timeline.Physics.PhysicsTriggerQuerySelection;
using PositionMode = BovineLabs.Timeline.Physics.PhysicsTriggerPositionMode;
using StatefulEventState = BovineLabs.Nerve.PhysicsStates.StatefulEventState;

// Builds a self-contained, deterministic Swept Trigger verification rig in its OWN dedicated scene pair
// (parent + baked SubScene), exactly like every other Showcase builder — it never touches the shared
// "Main Sub Scene". The rig: a target capsule + a weapon swept along an arc THROUGH it by a Position track,
// with a Swept Trigger track carrying Instantiate (spawn) + Condition (route event) clips on the active
// window, plus Force and Query cells. Idempotent: re-running rebuilds the dedicated scene from scratch.
// "Wire Swept On Player" is the one exception that MUST modify the shared scene (it attaches a blade to the
// live Player rig); "Purge Swept Rig From Open Scene(s)" reverses any rig objects (SweptTest_*, PlayerSwept_*)
// out of whatever scene is open — use it to clean the previously-committed pollution out of Main Sub Scene.
public static class SweptTriggerTestBuilder
{
    private const string Folder = "Assets/SweptTriggerTest";
    private const string ParentPath = Folder + "/SweptTriggerTest.unity";
    private const string SubPath = Folder + "/SweptTriggerTest_Sub.unity";
    private const string PlayablePath = Folder + "/SweptTriggerTest.playable";
    private const string MatPath = Folder + "/SweptTest_Mat.mat";
    private const string TargetMatPath = Folder + "/SweptTest_TargetMat.mat";

    // Only "Wire Swept On Player" and "Purge" touch this shared scene; Build uses its own dedicated scene.
    private const string SubSceneName = "Main Sub Scene";

    // Dedicated collision category for the test so the sweep only hits the test target.
    private const uint TestCategory = 1u << 9;

    // Test rig placed far from the existing content.
    private static readonly Vector3 Origin = new Vector3(30f, 1f, 0f);

    [MenuItem("Showcase/Build Swept Trigger Test")]
    public static void Build()
    {
        // --- Pick spawnable + condition assets (deterministic: prefer a known hit VFX, not an arbitrary first asset) ---
        var objDef = LoadByName<ObjectDefinition>("FizzleFX") ?? LoadFirst<ObjectDefinition>();
        var condition = LoadByName<ConditionEventObject>("OnTargetRooted") ?? LoadFirst<ConditionEventObject>();
        if (objDef == null)
        {
            Debug.LogError("SweptTriggerTest: no ObjectDefinition asset found to spawn. Aborting.");
            return;
        }

        EnsureFolder();

        // Build into a DEDICATED scene pair (parent + additive sub), never the shared Main Sub Scene — the
        // same pattern every other Showcase builder uses. The sub is wrapped as a baked SubScene at the end.
        DeleteDedicatedScenes();
        var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(parent, ParentPath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SetActiveScene(scene);

        // --- Target capsule (the thing we hit) ---
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.name = "SweptTest_Target";
        target.transform.position = Origin;
        target.transform.localScale = new Vector3(1f, 1f, 1f);
        Object.DestroyImmediate(target.GetComponent<Collider>());
        target.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(TargetMatPath, new Color(0.9f, 0.3f, 0.3f));

        var tShape = target.AddComponent<PhysicsShapeAuthoring>();
        tShape.SetCapsule(new CapsuleGeometryAuthoring
        {
            Height = 2f,
            Radius = 0.5f,
            Center = float3.zero,
            Orientation = quaternion.identity,
        });
        tShape.CollisionResponse = CollisionResponsePolicy.Collide;
        tShape.OverrideBelongsTo = true;
        tShape.BelongsTo = new PhysicsCategoryTags { Value = TestCategory };
        tShape.OverrideCollidesWith = true;
        tShape.CollidesWith = new PhysicsCategoryTags { Value = ~0u };
        var tBody = target.AddComponent<PhysicsBodyAuthoring>();
        tBody.MotionType = BodyMotionType.Static;
        target.AddComponent<LifeCycleAuthoring>();
        var tTargets = target.AddComponent<TargetsAuthoring>();
        tTargets.Owner = target;
        tTargets.Source = target;
        tTargets.Target = target;
        tTargets.Custom = target;
        SceneManager.MoveGameObjectToScene(target, scene);

        // --- Attacker (owner of the weapon, so ignoreTarget=Owner skips the wielder) ---
        var attacker = new GameObject("SweptTest_Attacker");
        attacker.transform.position = Origin + new Vector3(0f, 0f, -3f);
        attacker.AddComponent<LifeCycleAuthoring>();
        var aTargets = attacker.AddComponent<TargetsAuthoring>();
        aTargets.Owner = attacker;
        aTargets.Source = attacker;
        aTargets.Target = attacker;
        aTargets.Custom = attacker;
        SceneManager.MoveGameObjectToScene(attacker, scene);

        // --- Weapon (the swept volume), driven along an arc by a Position track ---
        var weapon = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        weapon.name = "SweptTest_Weapon";
        weapon.transform.position = Origin + new Vector3(-2.5f, 0f, 0f);
        weapon.transform.localScale = new Vector3(0.18f, 0.5f, 0.18f);
        Object.DestroyImmediate(weapon.GetComponent<Collider>());
        weapon.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(MatPath, new Color(0.5f, 0.8f, 1f));
        weapon.AddComponent<LifeCycleAuthoring>();
        var wTargets = weapon.AddComponent<TargetsAuthoring>();
        wTargets.Owner = attacker; // ignore the attacker, hit the target
        wTargets.Source = weapon;
        wTargets.Target = target;
        wTargets.Custom = weapon;
        AddSweptShape(weapon, 0.35f, 1.0f, TestCategory, 2);
        SceneManager.MoveGameObjectToScene(weapon, scene);

        // --- Timeline: Position arc (weapon) + Swept Trigger (Instantiate + Condition) ---
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, PlayablePath);

        // Position arc as blended waypoints: right -> through target -> left -> back. Blending between
        // consecutive World waypoints produces continuous motion (a single clip would just hold/teleport),
        // so the swept cast sees real travel crossing the target twice per loop.
        var posTrack = timeline.CreateTrack<PositionTrack>(null, "Swing");
        posTrack.ResetPositionOnDeactivate = false;
        AddWaypoint(posTrack, 0.0, 0.4, "right", Origin + new Vector3(2.5f, 0f, 0f), 0.0);
        AddWaypoint(posTrack, 0.4, 0.4, "left", Origin + new Vector3(-2.5f, 0f, 0f), 0.35);
        AddWaypoint(posTrack, 0.8, 0.4, "right2", Origin + new Vector3(2.5f, 0f, 0f), 0.35);

        // Swept Trigger: spawn + condition on the whole window (the swept system gates per-overlap).
        var sweptTrack = timeline.CreateTrack<SweptTrack>(null, "Swept");

        var instClip = sweptTrack.CreateClip<InstantiateClip>();
        instClip.start = 0.0;
        instClip.duration = 1.2;
        instClip.displayName = "spawn on hit";
        var inst = (InstantiateClip)instClip.asset;
        inst.objectDefinition = objDef;
        inst.triggerState = StatefulEventState.Enter;
        inst.ignoreTarget = TargetSlot.Owner;
        // Swept events carry NO real contact point, so the default MatchContactPoint silently spawns at the
        // blade<->target MIDPOINT (a VFX floating in mid-air). Spawn ON the victim instead for swept melee.
        inst.positionMode = PositionMode.MatchCollidedEntity;
        EditorUtility.SetDirty(inst);

        if (condition != null)
        {
            var condClip = sweptTrack.CreateClip<ConditionClip>();
            condClip.start = 0.0;
            condClip.duration = 1.2;
            condClip.displayName = "event on hit";
            var cond = (ConditionClip)condClip.asset;
            cond.triggerState = StatefulEventState.Enter;
            cond.collidesWith = new PhysicsCategoryTags { Value = TestCategory };
            cond.condition = condition;
            cond.value = 1;
            cond.routeTo = TargetSlot.Target;
            cond.ignoreTarget = TargetSlot.Owner;
            EditorUtility.SetDirty(cond);
        }

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1.2;
        EditorUtility.SetDirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) EditorUtility.SetDirty(tr);
        AssetDatabase.SaveAssets();

        // --- Director ---
        var dirGo = new GameObject("SweptTest_Director");
        SceneManager.MoveGameObjectToScene(dirGo, scene);
        var director = dirGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        var begin = dirGo.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;

        // Bind: Swing -> weapon.transform ; Swept -> weapon's SweptTriggerSourceAuthoring.
        foreach (var tr in timeline.GetOutputTracks())
        {
            if (tr.name == "Swing")
                director.SetGenericBinding(tr, weapon.transform);
            else if (tr.name == "Swept")
                director.SetGenericBinding(tr, weapon.GetComponent<SweptSource>());
        }

        EditorUtility.SetDirty(director);

        // Additional cells so EVERY main clip is exercised, not just spawn:
        // FORCE — a dynamic target gets knocked when the swing hits it (observable as displacement).
        BuildClipCell(scene, "SweptTest_Force", Origin + new Vector3(0f, 0f, 12f), true, (sweptTrack, weapon, tgt) =>
        {
            var c = sweptTrack.CreateClip<ForceClip>();
            c.start = 0.0; c.duration = 1.2; c.displayName = "knockback on hit";
            var f = (ForceClip)c.asset;
            f.triggerState = StatefulEventState.Enter;
            f.forceType = ForceType.Directional;
            f.mode = PhysForceMode.Impulse;
            f.magnitude = 8f;
            f.direction = new Vector3(0f, 0.3f, 1f);
            f.applyTo = TargetSlot.Target;
            f.ignoreTarget = TargetSlot.Owner;
            EditorUtility.SetDirty(f);
        });

        // QUERY — selects the contacted target into the weapon's Custom slot (observable as Targets.Custom).
        BuildClipCell(scene, "SweptTest_Query", Origin + new Vector3(0f, 0f, 24f), false, (sweptTrack, weapon, tgt) =>
        {
            var c = sweptTrack.CreateClip<QueryClip>();
            c.start = 0.0; c.duration = 1.2; c.displayName = "select on hit";
            var q = (QueryClip)c.asset;
            q.triggerState = StatefulEventState.Enter; // swept fast passes give Enter, rarely Stay

            q.collidesWith = new PhysicsCategoryTags { Value = TestCategory };
            q.selection = QuerySelection.Nearest;
            q.routeTo = TargetSlot.Custom;
            q.ignoreTarget = TargetSlot.Owner;
            EditorUtility.SetDirty(q);
        });

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, SubPath);
        AssetDatabase.SaveAssets();

        // Wrap the sub as a baked SubScene under the parent, then open the parent standalone.
        EditorSceneManager.SetActiveScene(parent);
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
        var subSceneGo = new GameObject("SweptTest SubScene");
        var subScene = subSceneGo.AddComponent<SubScene>();
        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);
        EditorSceneManager.MarkSceneDirty(parent);
        EditorSceneManager.SaveScene(parent);
        EditorSceneManager.CloseScene(scene, true);
        EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

        Debug.Log("SweptTriggerTest: built dedicated scene '" + ParentPath + "' | spawn=" + objDef.name +
                  " condition=" + (condition != null ? condition.name : "<none>") + " category=" + TestCategory +
                  " | open this scene and enter Play to verify. Cells: SweptTest_* (spawn / force / query). " +
                  "Never touches Main Sub Scene.");
    }

    [MenuItem("Showcase/Wire Swept On Player")]
    public static void WireOnPlayer()
    {
        var scene = SceneManager.GetSceneByName(SubSceneName);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"SweptTriggerTest: open '{SubSceneName}' (its SubScene) for editing first.");
            return;
        }

        EnsureFolder();

        // idempotent: clear any prior rig objects from this shared scene (roots + the bone-parented blade).
        Cleanup(scene);

        // deterministic: prefer a known hit VFX, not whatever ObjectDefinition happens to be first by GUID.
        var objDef = LoadByName<ObjectDefinition>("FizzleFX") ?? LoadFirst<ObjectDefinition>();
        if (objDef == null)
        {
            Debug.LogError("SweptTriggerTest: no ObjectDefinition to spawn.");
            return;
        }

        // Find the Player rig + its hand/weapon bone.
        GameObject player = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == "Player" && go.GetComponentInChildren<Animator>(true) != null)
            {
                player = go;
                break;
            }
        }

        if (player == null)
        {
            Debug.LogError("SweptTriggerTest: 'Player' rig not found.");
            return;
        }

        Transform bone = null;
        foreach (var t in player.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "mixamorig:RightSWORD")
            {
                bone = t;
                break;
            }
        }

        if (bone == null)
        {
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "mixamorig:RightHand")
                {
                    bone = t;
                    break;
                }
            }
        }

        if (bone == null)
        {
            Debug.LogError("SweptTriggerTest: no RightSWORD/RightHand bone under Player.");
            return;
        }

        // Blade on the hand bone — the user's idle-swing animation drives the bone, so the blade sweeps with it.
        var blade = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        blade.name = "PlayerSwept_Blade";
        blade.transform.SetParent(bone, false);
        blade.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        blade.transform.localScale = new Vector3(0.06f, 0.4f, 0.06f);
        Object.DestroyImmediate(blade.GetComponent<Collider>());
        blade.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(MatPath, new Color(0.5f, 0.8f, 1f));
        blade.AddComponent<LifeCycleAuthoring>();
        var bt = blade.AddComponent<TargetsAuthoring>();
        bt.Owner = player; // ignore the wielder
        bt.Source = blade;
        bt.Custom = blade;
        AddSweptShape(blade, 0.18f, 0.8f, TestCategory, 4);

        // Target capsule placed in front of the Player at swing height.
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.name = "PlayerSwept_Target";
        target.transform.position = player.transform.position + new Vector3(0.6f, 1.0f, 1.4f);
        Object.DestroyImmediate(target.GetComponent<Collider>());
        target.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(TargetMatPath, new Color(0.9f, 0.3f, 0.3f));
        var tShape = target.AddComponent<PhysicsShapeAuthoring>();
        tShape.SetCapsule(new CapsuleGeometryAuthoring { Height = 2f, Radius = 0.5f, Center = float3.zero, Orientation = quaternion.identity });
        tShape.CollisionResponse = CollisionResponsePolicy.Collide;
        tShape.OverrideBelongsTo = true;
        tShape.BelongsTo = new PhysicsCategoryTags { Value = TestCategory };
        tShape.OverrideCollidesWith = true;
        tShape.CollidesWith = new PhysicsCategoryTags { Value = ~0u };
        var tBody = target.AddComponent<PhysicsBodyAuthoring>();
        tBody.MotionType = BodyMotionType.Static;
        target.AddComponent<LifeCycleAuthoring>();
        var tt = target.AddComponent<TargetsAuthoring>();
        tt.Owner = target; tt.Source = target; tt.Target = target; tt.Custom = target;
        SceneManager.MoveGameObjectToScene(target, scene);

        // Director: Swept Trigger track, always-active Instantiate clip (the swing animation drives the sweep).
        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, Folder + "/PlayerSwept.playable");
        var sweptTrack = timeline.CreateTrack<SweptTrack>(null, "Swept");
        var clip = sweptTrack.CreateClip<InstantiateClip>();
        clip.start = 0.0;
        clip.duration = 1000.0; // always active; the animation's swing is what makes the blade actually hit
        clip.displayName = "spawn on hit";
        var inst = (InstantiateClip)clip.asset;
        inst.objectDefinition = objDef;
        inst.triggerState = StatefulEventState.Enter;
        inst.ignoreTarget = TargetSlot.Owner;
        // Swept events have NO contact point, so MatchContactPoint would spawn the VFX at the blade<->target
        // midpoint (floating in mid-air). Spawn ON the contacted target so the effect lands on the victim.
        inst.positionMode = PositionMode.MatchCollidedEntity;
        EditorUtility.SetDirty(inst);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1000.0;
        EditorUtility.SetDirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) EditorUtility.SetDirty(tr);
        AssetDatabase.SaveAssets();

        var dirGo = new GameObject("PlayerSwept_Director");
        SceneManager.MoveGameObjectToScene(dirGo, scene);
        var director = dirGo.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = true;
        director.extrapolationMode = DirectorWrapMode.Loop;
        var begin = dirGo.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;
        foreach (var tr in timeline.GetOutputTracks())
            director.SetGenericBinding(tr, blade.GetComponent<SweptSource>());
        EditorUtility.SetDirty(director);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("SweptTriggerTest: wired swept trigger on Player (bone=" + bone.name + ") spawn=" + objDef.name +
                  " target@" + target.transform.position + ". Objects: PlayerSwept_Blade, PlayerSwept_Target, PlayerSwept_Director.");
    }

    [MenuItem("Showcase/Clear Swept Trigger Test")]
    public static void Clear()
    {
        // Strip any rig objects from every open editable scene (the player-wired rig lives in the shared scene).
        var purged = PurgeOpenScenes();

        // Delete every generated asset under the rig folder (dedicated scenes, playables, materials) — not just
        // the one main playable, so repeated build/clear cycles never leave orphan .playable/.mat/.unity behind.
        var deleted = 0;
        if (AssetDatabase.IsValidFolder(Folder))
        {
            foreach (var typeFilter in new[] { "t:SceneAsset", "t:TimelineAsset", "t:Material" })
            {
                foreach (var guid in AssetDatabase.FindAssets(typeFilter, new[] { Folder }))
                {
                    if (AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid)))
                        deleted++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"SweptTriggerTest: cleared ({purged} scene object(s), {deleted} generated asset(s) removed).");
    }

    // Reverses the previously-committed pollution: removes every SweptTest_/PlayerSwept_ object from whatever
    // scene(s) are open (Main Sub Scene included) and saves. Run this once, then commit the cleaned scene.
    [MenuItem("Showcase/Purge Swept Rig From Open Scene(s)")]
    public static void Purge()
    {
        var purged = PurgeOpenScenes();
        Debug.Log(purged > 0
            ? $"SweptTriggerTest: purged {purged} swept-rig object(s) from open scene(s) and saved — commit the cleaned scene."
            : "SweptTriggerTest: no SweptTest_/PlayerSwept_ objects found in any open scene.");
    }

    // Strips every rig object out of all open, loaded, editable scenes and saves the ones that changed.
    private static int PurgeOpenScenes()
    {
        var total = 0;
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            var n = Cleanup(s);
            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                total += n;
            }
        }

        return total;
    }

    // Builds an independent swinging-weapon + target + director rig and lets the caller add the swept clip(s).
    private static void BuildClipCell(Scene scene, string prefix, Vector3 origin, bool dynamicTarget,
        System.Action<TrackAsset, GameObject, GameObject> configureClips)
    {
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.name = prefix + "_Target";
        target.transform.position = origin;
        Object.DestroyImmediate(target.GetComponent<Collider>());
        target.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(TargetMatPath, new Color(0.9f, 0.3f, 0.3f));
        var ts = target.AddComponent<PhysicsShapeAuthoring>();
        ts.SetCapsule(new CapsuleGeometryAuthoring { Height = 2f, Radius = 0.5f, Center = float3.zero, Orientation = quaternion.identity });
        ts.CollisionResponse = CollisionResponsePolicy.Collide;
        ts.OverrideBelongsTo = true;
        ts.BelongsTo = new PhysicsCategoryTags { Value = TestCategory };
        ts.OverrideCollidesWith = true;
        ts.CollidesWith = new PhysicsCategoryTags { Value = ~0u };
        var tb = target.AddComponent<PhysicsBodyAuthoring>();
        tb.MotionType = dynamicTarget ? BodyMotionType.Dynamic : BodyMotionType.Static;
        if (dynamicTarget)
        {
            tb.Mass = 1f;
            tb.GravityFactor = 0f;
            tb.LinearDamping = 0.05f;
        }

        target.AddComponent<LifeCycleAuthoring>();
        var tt = target.AddComponent<TargetsAuthoring>();
        tt.Owner = target; tt.Source = target; tt.Target = target; tt.Custom = target;
        SceneManager.MoveGameObjectToScene(target, scene);

        var attacker = new GameObject(prefix + "_Attacker");
        attacker.transform.position = origin + new Vector3(0f, 0f, -3f);
        attacker.AddComponent<LifeCycleAuthoring>();
        var at = attacker.AddComponent<TargetsAuthoring>();
        at.Owner = attacker; at.Source = attacker; at.Target = attacker; at.Custom = attacker;
        SceneManager.MoveGameObjectToScene(attacker, scene);

        var weapon = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        weapon.name = prefix + "_Weapon";
        weapon.transform.position = origin + new Vector3(-2.5f, 0f, 0f);
        weapon.transform.localScale = new Vector3(0.18f, 0.5f, 0.18f);
        Object.DestroyImmediate(weapon.GetComponent<Collider>());
        weapon.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMat(MatPath, new Color(0.5f, 0.8f, 1f));
        weapon.AddComponent<LifeCycleAuthoring>();
        var wt = weapon.AddComponent<TargetsAuthoring>();
        wt.Owner = attacker; wt.Source = weapon; wt.Target = target; wt.Custom = weapon;
        AddSweptShape(weapon, 0.35f, 1f, TestCategory, 2);
        SceneManager.MoveGameObjectToScene(weapon, scene);

        var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(timeline, Folder + "/" + prefix + ".playable");
        var posTrack = timeline.CreateTrack<PositionTrack>(null, "Swing");
        posTrack.ResetPositionOnDeactivate = false;
        AddWaypoint(posTrack, 0.0, 0.4, "right", origin + new Vector3(2.5f, 0f, 0f), 0.0);
        AddWaypoint(posTrack, 0.4, 0.4, "left", origin + new Vector3(-2.5f, 0f, 0f), 0.35);
        AddWaypoint(posTrack, 0.8, 0.4, "right2", origin + new Vector3(2.5f, 0f, 0f), 0.35);
        var sweptTrack = timeline.CreateTrack<SweptTrack>(null, "Swept");
        configureClips(sweptTrack, weapon, target);
        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = 1.2;
        EditorUtility.SetDirty(timeline);
        foreach (var tr in timeline.GetOutputTracks()) EditorUtility.SetDirty(tr);
        AssetDatabase.SaveAssets();

        var dirGo = new GameObject(prefix + "_Director");
        SceneManager.MoveGameObjectToScene(dirGo, scene);
        var dir = dirGo.AddComponent<PlayableDirector>();
        dir.playableAsset = timeline;
        dir.playOnAwake = true;
        dir.extrapolationMode = DirectorWrapMode.Loop;
        var begin = dirGo.AddComponent<TimelineBeginAuthoring>();
        begin.Mode = TimelineBeginMode.OnLoad;
        begin.DelaySeconds = 0f;
        foreach (var tr in timeline.GetOutputTracks())
        {
            if (tr.name == "Swing")
                dir.SetGenericBinding(tr, weapon.transform);
            else if (tr.name == "Swept")
                dir.SetGenericBinding(tr, weapon.GetComponent<SweptSource>());
        }

        EditorUtility.SetDirty(dir);
    }

    // Sets up a swept source: a SweptTriggerSourceAuthoring + a DISABLED PhysicsShapeAuthoring capsule (the
    // swept source reads the shape's geometry; disabled = never baked into a real collider).
    private static void AddSweptShape(GameObject go, float radius, float length, uint collidesWith, int subSteps)
    {
        var src = go.AddComponent<SweptSource>();
        src.subSteps = subSteps;

        var shape = go.AddComponent<PhysicsShapeAuthoring>();
        shape.SetCapsule(new CapsuleGeometryAuthoring
        {
            Center = float3.zero,
            Height = length + (2f * radius),
            Radius = radius,
            Orientation = quaternion.identity,
        });
        shape.OverrideCollidesWith = true;
        shape.CollidesWith = new PhysicsCategoryTags { Value = collidesWith };
        shape.enabled = false;
    }

    private static void AddWaypoint(PositionTrack track, double start, double dur, string name, Vector3 world, double blendIn)
    {
        var clip = track.CreateClip<PositionClip>();
        clip.start = start;
        clip.duration = dur;
        clip.displayName = name;
        clip.blendInDuration = blendIn;
        var a = (PositionClip)clip.asset;
        a.Type = PositionType.World;
        a.Position = world;
        EditorUtility.SetDirty(a);
    }

    // Removes every rig object (SweptTest_*, PlayerSwept_*) from a scene, including the bone-parented
    // PlayerSwept_Blade nested under the live Player rig. Returns the number destroyed.
    private static int Cleanup(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;

        var toDestroy = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (IsRigName(root.name))
            {
                toDestroy.Add(root);
                continue;
            }

            // Descend for rig objects parented under a non-rig hierarchy (e.g. the blade under the Player rig).
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && IsRigName(t.name))
                    toDestroy.Add(t.gameObject);
            }
        }

        var removed = 0;
        foreach (var go in toDestroy)
        {
            if (go == null)
                continue;

            Object.DestroyImmediate(go);
            removed++;
        }

        return removed;
    }

    private static bool IsRigName(string n) =>
        n.StartsWith("SweptTest_") || n.StartsWith("PlayerSwept_");

    private static void DeleteDedicatedScenes()
    {
        foreach (var p in new[] { ParentPath, SubPath })
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null)
                AssetDatabase.DeleteAsset(p);
        }
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "SweptTriggerTest");
    }

    private static Material LoadOrCreateMat(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            mat = new Material(sh);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static T LoadFirst<T>() where T : Object
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
        return guids.Length == 0 ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static T LoadByName<T>(string name) where T : Object
    {
        var guids = AssetDatabase.FindAssets(name + " t:" + typeof(T).Name);
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var a = AssetDatabase.LoadAssetAtPath<T>(p);
            if (a != null && a.name == name) return a;
        }

        return null;
    }
}
