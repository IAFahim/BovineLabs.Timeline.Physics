// Designer showcase for the Intent-vs-External motion channel — the REAL way.
// Menu: Showcase/Build Motion Channels. Builds a scene pair under Assets/Samples/MotionChannelShowcase.
//
// THE LESSON: a knockback comes from an ATTACKER's hitbox touching you — NOT a clip on your own timeline.
//   Each lane = an attacker that sweeps into a VICTIM who is braking (a drag clip = the victim's OWN move).
//   The knockback clip lives on the ATTACKER's trigger, targets the victim, and the ONLY difference between
//   the two lanes is its `channel`:
//     - INTENT   : the victim's own brake EATS the hit  -> barely moves (the bug).
//     - EXTERNAL : the hit IGNORES the victim's brake    -> flies (the fix).
// Mirrors the proven trigger recipe in Sample~/Physics Showcase/Editor/PhysicsShowcaseBuilder.cs.
using System.Collections.Generic;
using TMPro;
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
using Space = BovineLabs.Reaction.Data.Core.Target;
using TargetSlot = BovineLabs.Reaction.Data.Core.Target;
using TargetsAuthoring = BovineLabs.Reaction.Authoring.Core.TargetsAuthoring;
using TriggerAuthoring = BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring;
using VelocityTrack = BovineLabs.Timeline.Physics.Authoring.PhysicsVelocityTrack;
using VelocityClip = BovineLabs.Timeline.Physics.Authoring.PhysicsVelocityClip;
using VelocityMode = BovineLabs.Timeline.Physics.Data.PhysicsVelocityMode;
using DragTrack = BovineLabs.Timeline.Physics.Authoring.PhysicsDragTrack;
using DragClip = BovineLabs.Timeline.Physics.Authoring.PhysicsDragClip;
using TriggerTrack = BovineLabs.Timeline.Physics.Authoring.StatefulTriggerTrack;
using TriggerForceClip = BovineLabs.Timeline.Physics.Authoring.PhysicsTriggerForceClip;
using TriggerForceType = BovineLabs.Timeline.Physics.PhysicsTriggerForceType;
using EventState = BovineLabs.Core.PhysicsStates.StatefulEventState;
using ForceMode = BovineLabs.Timeline.Physics.PhysicsForceMode;
using Channel = BovineLabs.Timeline.Physics.MotionChannel;
using TimelineBeginAuthoring = BovineLabs.Timeline.Core.Authoring.TimelineBeginAuthoring;
using TimelineBeginMode = BovineLabs.Timeline.Core.Authoring.TimelineBeginMode;

namespace BovineLabs.Showcase.Editor
{
    public static class MotionChannelShowcaseBuilder
    {
        private const string Folder = "Assets/Samples/MotionChannelShowcase";
        private const string TimelineFolder = Folder + "/Timelines";
        private const string ParentPath = Folder + "/MotionChannelShowcase.unity";
        private const string SubPath = Folder + "/MotionChannelShowcase_Sub.unity";

        private static readonly Vector3 CameraPos = new Vector3(3f, 13f, -16f);
        private static readonly Color VictimIntentColor = new Color(0.30f, 0.55f, 0.95f); // blue = braked
        private static readonly Color VictimExternalColor = new Color(0.95f, 0.35f, 0.20f); // red = flies
        private static readonly Color AttackerColor = new Color(0.85f, 0.80f, 0.25f);       // yellow fist
        private static readonly Color PadColor = new Color(0.20f, 0.22f, 0.27f);

        private const uint CatBody = 1u << 1;
        private const uint CatTrigger = 1u << 3;

        // Identical on both lanes — only the channel differs.
        private const float Brake = 80f;        // victim's own heavy drag (its brace/skid)
        private const float HitMag = 16f;       // knockback magnitude (negative radial = pushed away)
        private const float ChargeSpeed = 5f;   // how fast the attacker sweeps in
        private const float VictimX = 2f;
        private const float AttackerX = -3.5f;
        private const double Length = 4.0;

        private const float IntentZ = 0f;
        private const float ExternalZ = 6f;

        private static Scene activeSub;

        private enum BindKind { Body, Trigger }

        private sealed class TrackBind
        {
            public string TrackName;
            public string BindName;
            public BindKind Kind;
        }

        private sealed class CellWire
        {
            public string DirectorName;
            public string TimelinePath;
            public List<TrackBind> Binds;
        }

        private static readonly List<CellWire> Wires = new List<CellWire>();

        [MenuItem("Showcase/Build Motion Channels")]
        public static void Build()
        {
            Wires.Clear();
            EnsureFolders();
            // Release any open showcase scene first, so ResetAssets' DeleteAsset succeeds and the rebuilt
            // scenes get FRESH GUIDs — overwriting a still-open scene keeps its GUID + stale entity bake.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ResetAssets();

            var parent = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(parent, ParentPath);
            var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(sub);
            activeSub = sub;

            MakePad("Ground", new Vector3(3f, -0.4f, 3f), new Vector3(34f, 0.8f, 16f));

            BuildLane("Intent", IntentZ, VictimIntentColor, Channel.Intent);
            BuildLane("External", ExternalZ, VictimExternalColor, Channel.External);

            EditorSceneManager.SaveScene(sub, SubPath);
            EditorSceneManager.SetActiveScene(parent);
            EditorSceneManager.CloseScene(sub, true);

            // Reopen the sub to wire the director bindings (scene-side), then save.
            sub = EditorSceneManager.OpenScene(SubPath, OpenSceneMode.Additive);
            EditorSceneManager.SetActiveScene(sub);
            foreach (var w in Wires)
            {
                WireCell(w);
            }

            EditorSceneManager.MarkSceneDirty(sub);
            EditorSceneManager.SaveScene(sub);

            EditorSceneManager.SetActiveScene(parent);
            BuildParent();
            EditorSceneManager.SaveScene(parent);
            EditorSceneManager.CloseScene(sub, true);
            EditorSceneManager.OpenScene(ParentPath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SubPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("MotionChannelShowcase: built attacker->victim demo at " + ParentPath + " — press Play.");
        }

        private static void BuildLane(string lane, float z, Color victimColor, Channel channel)
        {
            // --- Victim: braking with its OWN timeline (a heavy drag clip). ---
            var victim = MakeBall(lane + "_Victim", new Vector3(VictimX, 0.75f, z), 0.5f, victimColor);
            SetBodyFilter(victim, CatBody, CatTrigger); // detected BY the attacker's trigger

            var vTimeline = NewTimeline(lane + "_Victim");
            var brakeTrack = vTimeline.CreateTrack<DragTrack>(null, "Brake");
            var brakeClip = brakeTrack.CreateClip<DragClip>();
            brakeClip.start = 0.0;
            brakeClip.duration = Length;
            brakeClip.displayName = "brake " + Brake;
            var drag = (DragClip)brakeClip.asset;
            drag.linearDrag = Brake;
            drag.angularDrag = 5f;
            EditorUtility.SetDirty(drag);
            FinishTimeline(vTimeline);
            MakeDirector(lane + "_Victim_Director");
            Wires.Add(new CellWire
            {
                DirectorName = lane + "_Victim_Director",
                TimelinePath = AssetDatabase.GetAssetPath(vTimeline),
                Binds = new List<TrackBind> { new TrackBind { TrackName = "Brake", BindName = lane + "_Victim", Kind = BindKind.Body } },
            });

            // --- Attacker: a hitbox that sweeps into the victim and delivers the knockback. ---
            var attacker = MakeAttacker(lane + "_Attacker", new Vector3(AttackerX, 0.75f, z));

            var aTimeline = NewTimeline(lane + "_Attacker");
            // 1) Charge in (+X) toward the victim — the attacker's own movement.
            var chargeTrack = aTimeline.CreateTrack<VelocityTrack>(null, "Charge");
            var chargeClip = chargeTrack.CreateClip<VelocityClip>();
            chargeClip.start = 0.0;
            chargeClip.duration = Length;
            chargeClip.displayName = "charge +X";
            var vel = (VelocityClip)chargeClip.asset;
            vel.mode = VelocityMode.SetContinuous;
            vel.space = Space.None;
            vel.linearVelocity = new Vector3(ChargeSpeed, 0f, 0f);
            EditorUtility.SetDirty(vel);
            // 2) The hit: on contact, knock the contacted body away. ONLY the channel differs per lane.
            var hitTrack = aTimeline.CreateTrack<TriggerTrack>(null, "Hit");
            var hitClip = hitTrack.CreateClip<TriggerForceClip>();
            hitClip.start = 0.0;
            hitClip.duration = Length;
            hitClip.displayName = channel + " hit";
            var hit = (TriggerForceClip)hitClip.asset;
            hit.triggerState = EventState.Enter;   // one punch, on contact
            hit.forceType = TriggerForceType.Radial;
            hit.mode = ForceMode.Impulse;
            hit.magnitude = -HitMag;               // negative radial = pushed AWAY from the attacker
            hit.applyTo = TargetSlot.Target;       // the body that entered = the victim
            hit.ignoreTarget = TargetSlot.Owner;   // never the attacker itself
            hit.channel = channel;                 // <-- THE ONLY DIFFERENCE BETWEEN LANES
            EditorUtility.SetDirty(hit);
            FinishTimeline(aTimeline);
            MakeDirector(lane + "_Attacker_Director");
            Wires.Add(new CellWire
            {
                DirectorName = lane + "_Attacker_Director",
                TimelinePath = AssetDatabase.GetAssetPath(aTimeline),
                Binds = new List<TrackBind>
                {
                    new TrackBind { TrackName = "Charge", BindName = lane + "_Attacker", Kind = BindKind.Body },
                    new TrackBind { TrackName = "Hit", BindName = lane + "_Attacker", Kind = BindKind.Trigger },
                },
            });
        }

        private static void WireCell(CellWire w)
        {
            var director = GameObject.Find(w.DirectorName).GetComponent<PlayableDirector>();
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(w.TimelinePath);
            director.playableAsset = timeline;
            foreach (var track in timeline.GetOutputTracks())
            {
                var bind = w.Binds.Find(b => b.TrackName == track.name);
                if (bind == null) continue;
                var go = GameObject.Find(bind.BindName);
                Object value = bind.Kind == BindKind.Trigger
                    ? go.GetComponent<TriggerAuthoring>()
                    : go.GetComponent<PhysicsBodyAuthoring>();
                director.SetGenericBinding(track, value);
            }

            EditorUtility.SetDirty(director);
        }

        private static GameObject MakeBall(string name, Vector3 pos, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, radius * 2f);
            Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, color);

            var shape = go.AddComponent<PhysicsShapeAuthoring>();
            shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity);
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = MakeTags(CatBody);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = MakeTags(CatBody | CatTrigger);

            var body = go.AddComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Dynamic;
            body.Mass = 1f;
            body.GravityFactor = 0f;
            body.LinearDamping = 0f;
            body.AngularDamping = 0f;

            SceneManager.MoveGameObjectToScene(go, activeSub);
            return go;
        }

        private static GameObject MakeAttacker(string name, Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, AttackerColor);

            var shape = go.AddComponent<PhysicsShapeAuthoring>();
            shape.SetSphere(new SphereGeometry { Center = float3.zero, Radius = 0.5f }, quaternion.identity);
            shape.OverrideCollisionResponse = true;
            shape.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents; // passes through, fires events
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = MakeTags(CatTrigger);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = MakeTags(CatBody);

            var body = go.AddComponent<PhysicsBodyAuthoring>();
            body.MotionType = BodyMotionType.Dynamic;
            body.Mass = 1f;
            body.GravityFactor = 0f;
            body.LinearDamping = 0f;
            body.AngularDamping = 0f;

            go.AddComponent<TriggerAuthoring>();
            var targets = go.AddComponent<TargetsAuthoring>();
            targets.Owner = go;

            SceneManager.MoveGameObjectToScene(go, activeSub);
            return go;
        }

        private static GameObject MakePad(string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<UnityEngine.Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = MakeMaterial(name, PadColor);
            SceneManager.MoveGameObjectToScene(go, activeSub);
            return go;
        }

        private static void BuildParent()
        {
            RenderSettings.fog = false;
            // BringInBootstrap();   // TEMP DISABLED: testing whether the bootstrap copy broke timeline activation
            EnsureCameraFramed();

            var lightGo = new GameObject("Showcase Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;

            Label("Title", "MOTION CHANNELS — a hit comes from the ATTACKER, not your own timeline", new Vector3(3f, 10f, 3f), 1.7f, Color.white);
            Label("ExternalLabel", "EXTERNAL  →  the hit IGNORES your brake (you fly)", new Vector3(3f, 7.6f, ExternalZ), 1.3f, VictimExternalColor);
            Label("IntentLabel", "INTENT  →  your own brake EATS the hit (you barely move)", new Vector3(3f, 5.2f, IntentZ), 1.3f, VictimIntentColor);
            Label("Hint", "yellow = attacker (its hitbox carries the knockback clip)   ·   blue/red = victim, braking the whole time", new Vector3(3f, 2.8f, -3f), 0.85f, new Color(0.85f, 0.88f, 0.95f));

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubPath);
            var subSceneGo = new GameObject("Showcase SubScene");
            var subScene = subSceneGo.AddComponent<SubScene>();
            subScene.SceneAsset = sceneAsset;
            subScene.AutoLoadScene = true;
            EditorUtility.SetDirty(subScene);
        }

        // Copy the project's bootstrap root so the showcase scene has the singletons play mode needs.
        private static void BringInBootstrap()
        {
            const string mainPath = "Assets/Scenes/Main Scene.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(mainPath) == null) return;
            var parent = EditorSceneManager.GetActiveScene();
            var main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Additive);
            foreach (var root in main.GetRootGameObjects())
            {
                if (root.name != "Required In Scene") continue;
                var copy = Object.Instantiate(root);
                copy.name = "Required In Scene";
                SceneManager.MoveGameObjectToScene(copy, parent);
                break;
            }

            EditorSceneManager.CloseScene(main, true);
        }

        private static void EnsureCameraFramed()
        {
            var cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }

            cam.transform.position = CameraPos;
            cam.transform.rotation = Quaternion.Euler(33f, 0f, 0f);
            cam.fieldOfView = 60f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            EditorUtility.SetDirty(cam);
        }

        private static void Label(string name, string text, Vector3 pos, float fontSize, Color color)
        {
            var holder = new GameObject(name);
            holder.transform.position = pos;
            holder.transform.rotation = Quaternion.LookRotation(pos - CameraPos, Vector3.up);
            var tmp = holder.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(26f, 3f);
            tmp.fontStyle = FontStyles.Bold;
        }

        private static TimelineAsset NewTimeline(string name)
        {
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelineFolder + "/" + name + ".playable");
            return timeline;
        }

        private static void FinishTimeline(TimelineAsset timeline)
        {
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Length;
            EditorUtility.SetDirty(timeline);
            foreach (var tr in timeline.GetOutputTracks()) EditorUtility.SetDirty(tr);
            AssetDatabase.SaveAssets();
        }

        private static void MakeDirector(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, activeSub);
            var director = go.AddComponent<PlayableDirector>();
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;
            // Explicit auto-start: TimelineBeginBaker honors this (Mode=OnLoad => TimelinePlayRequest enabled)
            // regardless of how playOnAwake serializes through the build/rebind flow.
            go.AddComponent<TimelineBeginAuthoring>().Mode = TimelineBeginMode.OnLoad;
        }

        private static void SetBodyFilter(GameObject go, uint belongsTo, uint collidesWith)
        {
            var shape = go.GetComponent<PhysicsShapeAuthoring>();
            shape.OverrideBelongsTo = true;
            shape.BelongsTo = MakeTags(belongsTo);
            shape.OverrideCollidesWith = true;
            shape.CollidesWith = MakeTags(collidesWith);
            EditorUtility.SetDirty(shape);
        }

        private static PhysicsCategoryTags MakeTags(uint value)
        {
            return new PhysicsCategoryTags { Value = value };
        }

        private static UnityEngine.Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new UnityEngine.Material(shader) { name = name + "_Mat" };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Samples")) AssetDatabase.CreateFolder("Assets", "Samples");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Samples", "MotionChannelShowcase");
            if (!AssetDatabase.IsValidFolder(TimelineFolder)) AssetDatabase.CreateFolder(Folder, "Timelines");
        }

        private static void ResetAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(TimelineFolder) != null)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:TimelineAsset", new[] { TimelineFolder }))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            foreach (var p in new[] { ParentPath, SubPath })
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(p) != null) AssetDatabase.DeleteAsset(p);
            }
        }
    }
}
