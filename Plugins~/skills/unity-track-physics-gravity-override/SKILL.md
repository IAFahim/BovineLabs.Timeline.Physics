---
name: unity-track-physics-gravity-override
description: Master of PhysicsGravityOverrideTrack + PhysicsGravityOverrideClip (package BovineLabs.Timeline.Physics) — blended while-timeline-active gravity scaling via PhysicsGravityFactor add/mutate, the add-path one-step latency, capture poisoning. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "zero-g / reverse gravity / moon gravity during this clip".
---

# PhysicsGravityOverrideTrack specialist

## 1. SCOPE

You are the specialist for **`PhysicsGravityOverrideTrack`** and
**`PhysicsGravityOverrideClip`** from the package `BovineLabs.Timeline.Physics`
(namespace `BovineLabs.Timeline.Physics.Authoring.Gravities`). Scope: exactly
this track family — authoring the track/clips in a `.playable` TimelineAsset,
wiring a SubScene PlayableDirector (GameObject binding), and the runtime
gravity-override semantics. Physics bodies/stages belong to a stage/physics-setup
specialist — a missing physics body is a missing-prerequisite report, never something you
create. Family patterns live in `unity-track-physics-filter-override` (its
**PHYSICS FAMILY SHARED PATTERNS** section — cite it, don't re-derive);
kinematic freezing in `unity-track-physics-kinematic-override` (see its CONFLICT
MATRIX before ever sharing a body with a kinematic track).
Behave per unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say
where a fact was PROVEN, not where it applies. (All verified vex-ee 2026-06 via
reflection dumps, package-source reads via `File.ReadAllText` inside exec, raw
YAML reads, fresh-load read-backs through `unity-cli exec`; no play mode —
runtime claims are source-derived.)

Distinctions from the Filter sibling:
1. **REAL blending** — `ClipCaps.Blending`: every clip bakes a `ClipWeight`,
   overlaps genuinely lerp through `PhysicsGravityOverrideMixer` (Filter's
   `ClipCaps.None` = first-writer races, dead-code mixer).
2. **PhysicsProducerGroup** — the apply system FEEDS the simulation (runs BEFORE
   `PhysicsSystemGroup`; Filter's runs after, in the modifier group).
3. **Component add/mutate/remove** — plain IComponentData, not a blob mutation;
   **NO ForceUnique requirement**, no shared-blob warning — but the ADD path is
   one fixed step latent.

| Type | Facts |
|---|---|
| `PhysicsGravityOverrideTrack` | `BovineLabs.Timeline.Physics.Authoring.Gravities`, asm `BovineLabs.Timeline.Physics.Authoring`, sealed, EMPTY body, base `DOTSTrack`. `[TrackClipType(typeof(PhysicsGravityOverrideClip))]`, `[TrackBindingType(typeof(GameObject))]`, `[TrackColor(0.2,0.6,0.8)]`, `[DisplayName("BovineLabs/Physics/Gravity Override")]`. |
| `PhysicsGravityOverrideClip` | sealed, base `DOTSClip`, `ITimelineClipAsset`, **`clipCaps => ClipCaps.Blending`** (contrast Filter's None), `duration => 1` (seed only). |
| `PhysicsGravityOverrideData` | `BovineLabs.Timeline.Physics`, asm `...Physics.Data`, IComponentData: `float GravityScale; bool RestoreOnExit;` |
| `PhysicsGravityOverrideAnimated` | `IAnimatedComponent<PhysicsGravityOverrideData>` — `AuthoredData` + `Value`, on the CLIP entity. |
| `ActiveGravityOverride` | IComponentData + **IEnableableComponent**: `PhysicsGravityOverrideData Config` — on the BINDING entity, added DISABLED at bake. |
| `PhysicsGravityOverrideState` | IComponentData: `bool Fired; float OriginalGravityScale; bool AddedComponent;` — on the BINDING entity. |
| `PhysicsGravityOverrideMixer` | `IMixer<PhysicsGravityOverrideData>` — REAL math: `GravityScale = math.lerp(a.GravityScale, b.GravityScale, s)`, `RestoreOnExit = s >= 0.5f ? b.RestoreOnExit : a.RestoreOnExit`; `Add(a,b) => b`. |
| `Unity.Physics.PhysicsGravityFactor` | `float Value` — the component the apply system adds/mutates/removes. |
| Systems | `PhysicsGravityOverrideTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`, per rendered frame) produces the enabled `ActiveGravityOverride{Config}` (enable via the **BeginSimulation** ECB — effective next frame); `PhysicsGravityOverrideApplySystem` (**`PhysicsProducerGroup`** = FixedStep BEFORE `PhysicsSystemGroup`; query uses `IgnoreComponentEnabledState`) consumes it against `PhysicsGravityFactor`. |

Clip fields (camelCase, fresh-instance reflection): `gravityScale` float,
default `1` — gravity multiplier, 1=normal, 0=zero-G, negative=reversed,
**UNCLAMPED**; `restoreOnExit` bool, default `True` — restore/remove at
override-REGIME end (timeline deactivation, NOT clip end).
`DEFAULTS|gravityScale=1|restoreOnExit=True|duration=1|clipCaps=Blending` (quoted).

YAML (raw-read verified): floats plain, bools as 1/0. Overlaps generate REAL
blend YAML (`m_BlendOutDuration`/`m_BlendInDuration` + populated mix curves;
ClipCaps.None tracks produce none). The deciding bake line, quoted from the
timeline package's `ClipBaker.cs`: `if ((clip.clipCaps & ClipCaps.Blending) != 0)
{ context.Baker.AddComponent(clipEntity, new ClipWeight { Value = 1 }); }`

**The conditional Unity.Physics bake rule (which path will YOUR body take?)** —
quoted from `PhysicsBodyBakingSystem.cs` (the COMPILED copy — in vex-ee
`com.unity.physics.custom`, NOT the PackageCache copy; Samples~ are not compiled):

```csharp
if (authoring.MotionType == BodyMotionType.Dynamic) {
    AddComponent(entity, new PhysicsDamping { ... });
    if (authoring.GravityFactor != 1)
        AddComponent(entity, new PhysicsGravityFactor { Value = authoring.GravityFactor });
}
else if (authoring.MotionType == BodyMotionType.Kinematic)
    AddComponent(entity, new PhysicsGravityFactor { Value = 0 });
```

Dynamic + authored GravityFactor=1 bakes NO `PhysicsGravityFactor` → clips take
the **ADD path** (`AddedComponent=true`, one-step latent). Factor≠1 or Kinematic
(always baked factor 0) → **MUTATE** in place (same-tick). Discover in §3.4.

Bake (quoted from `PhysicsGravityOverrideClip.Bake`): unconditional — no guard,
no LogError; adds `PhysicsGravityOverrideAnimated{AuthoredData}` via
`PhysicsGravityOverrideBuilder.ApplyTo`, then `base.Bake`. **NO bake-time
failure mode.** Binding-entity pair from the central `PhysicsTimelineBakingSystem`
(identical family shape to Filter's quoted loop with Gravity types substituted;
same `Entity.Null` continue — unbound track = total silent no-op).

### Runtime semantics (source-quoted)

The track system runs the family kernel per rendered frame (filter skill,
patterns 4–5; `ResetStateTrackJob` resets State to `{Fired=false,
AddedComponent=false, OriginalGravityScale=1}` only while `ActiveGravityOverride`
is disabled) — but every gravity clip bakes a `ClipWeight`, so overlaps go
through the 4-slot weighted register and `WriteActiveJob` writes `Config =
JobHelpers.Blend<PhysicsGravityOverrideData, PhysicsGravityOverrideMixer>(ref
mixData, default)` — a genuine weighted lerp — then ECB-enables
`ActiveGravityOverride` via the BeginSimulation ECB; `DisableStaleTrackJob`
disables only on the timeline-deactivation edge.
`PhysicsGravityOverrideApplySystem` runs the Fired machine per fixed step:
**enter** — component present → capture `OriginalGravityScale`, write
`Value = Config.GravityScale` in place, `AddedComponent=false`; absent →
`OriginalGravityScale=1`, `AddedComponent=true`, `ECB.AddComponent(chunkIndex,
entity, new PhysicsGravityFactor{Value})` on the **EndFixedStepSimulation** ECB;
**stay** — re-assert in place, or re-add via ECB if (externally) missing;
**exit** (timeline deactivation, last-written Config) — `RestoreOnExit` →
`AddedComponent ? ECB.RemoveComponent<PhysicsGravityFactor>` : in-place restore
of the captured original (third branch re-ADDS the original if the component
vanished mid-run); `RestoreOnExit=false` → component and value left permanently.

**Mental model:** same regime shape as Filter (family pattern 5) — first gravity
clip enters at its first frame, gaps hold the last blended Config (stay keeps
re-asserting), exit fires once at timeline deactivation with the LAST clip's
restore flag deciding for the whole run — but the target is a plain
IComponentData add/mutate/remove, not a blob, so no ForceUnique requirement, and
overlapping clips genuinely lerp instead of racing.

### Traps & DO/DON'T (each source-proven, vex-ee 2026-06)

- **DO know which path your body takes — add vs mutate, with exact-archetype
  recovery on remove** — quoted `OnEnter`: `hasGravityFactor` → capture
  `gravityFactors[i].Value`, `AddedComponent=false`, in-place write (same-tick);
  else → `OriginalGravityScale=1`, `AddedComponent=true`, `ECB.AddComponent(...)`
  (one-step latent). Exit symmetry: `AddedComponent ?
  ECB.RemoveComponent<PhysicsGravityFactor>() :` in-place restore. An add-path
  body returns to its exact baked archetype on restore.
- **DON'T file the stay-path duplicate-add as a bug — adjudicated NOT A BUG** —
  (1) the apply system queues on `EndFixedStepSimulationEntityCommandBufferSystem`
  (`[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]`) —
  commands queued during fixed step N play back at the END of step N, so the
  "absent window" closes before the apply system's next update; (2) enter and
  stay are exclusive branches of one per-entity dispatch — the stay re-add is
  reachable only if an external actor removes the component mid-regime:
  deliberate self-healing (mirroring the exit path's re-add-original third
  branch); (3) with-value `EntityCommandBuffer.AddComponent<T>` is documented
  *"At playback, if the entity already has this type of component, the value
  will just be set"* — idempotent, never a throw.
- **DO expect the REAL quirk instead: one-fixed-step latency on the ADD path** —
  the apply system runs in `PhysicsProducerGroup` (before the step), but the
  EndFixedStep ECB plays back AFTER it — on the enter tick of a component-less
  body, that tick's physics step still integrates with implicit factor 1. The
  mutate path writes in place before the step: effective the same tick.
  Asymmetric first-tick behavior; negligible at 60Hz, but real.
- **DO overlap gravity clips — same family as Filter, OPPOSITE overlap rule** —
  the ClipBaker line adds `ClipWeight` only when caps include Blending, so
  gravity clips take the 4-slot weight-sorted register and
  `PhysicsGravityOverrideMixer.Lerp` actually runs (at a 50/50 point,
  `math.lerp(a, b, 0.5)`). `RestoreOnExit` blends discretely
  (`s >= 0.5f ? b : a`). >4 simultaneous gravity clips on one binding: lowest
  weight silently dropped.
- **DO use negative/extreme scales freely — UNCLAMPED everywhere** — negative
  values serialize verbatim (raw YAML verified); no clamp in clip, mixer, or
  apply system. It scales the world gravity vector, it does not replace
  velocity: a falling body under scale −1 decelerates, then rises.
- **DON'T expect normal gravity in gaps — gaps hold the last blended value, not
  neutral** — `DisableStaleTrackJob` keys off `TimelineActivePrevious &&
  !TimelineActive`, never clip end; stay re-asserts the last Config every fixed
  step through every gap.
- **DON'T let a restoreOnExit=false clip near a body you ever want back —
  timeline-end restore + capture poisoning** — exit runs ONCE with the LAST
  clip's Config; `RestoreOnExit=false` leaves component and value permanently.
  **Next run**: the component now EXISTS → run 2 takes the MUTATE path — enter
  captures the mutated value as `OriginalGravityScale`, `AddedComponent=false`;
  even all-restore=true clips now "restore" the mutated value, and remove never
  fires. Undo needs a compensating clip or external `RemoveComponent`. (Within
  one run capture stays safe: Fired=true blocks re-capture; ResetStateTrackJob
  is gated on Active being disabled.)
- **DON'T be surprised by a component the authoring never had — bake/runtime
  divergence** — restoreOnExit=false leaves a PERMANENT `PhysicsGravityFactor`
  on an entity whose baked archetype never authored one; "why does this body
  have gravity factor 0.5? The PhysicsBodyAuthoring says 1" has no
  authoring-side answer — the timeline did it. Also flips the next run's branch
  from add to mutate.
- **DON'T share a body with a PhysicsKinematicOverrideTrack in the same
  timeline** — `zeroGravity` writes the SAME component; see the kinematic
  skill's CONFLICT MATRIX (orphaned adds, config-blind exit clobbers).
- **DO note the silence profile (family rule 7 holds)** — bake unconditional;
  unbound track = `Entity.Null` continue = total silent no-op; runtime has NO
  loud failure at all (no blob, no ForceUnique, no analogue of Filter's
  shared-blob warning). A clean console proves nothing.

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem;
never play mode. unity-cli Safe Loop on every mutation. Names below are
parameters — discover them in THIS project; never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideTrack, BovineLabs.Timeline.Physics.Authoring");
return t == null
    ? "MISSING_PREREQUISITE|PhysicsGravityOverrideTrack not found - package BovineLabs.Timeline.Physics is absent in this project"
    : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
```

**3.2 Scene + SubScene(s):** unity-cli First Command → record `parentScenePath`
and candidate `subScenePath`(s).

**3.3 Directors in the SubScene** (read-only additive open, restore parent
after): `FindObjectsByType<UnityEngine.Playables.PlayableDirector>(
FindObjectsInactive.Include, FindObjectsSortMode.None)` — print per director:
hierarchy path, scene.path, playableAsset (path or null), sibling components
(timeline-reference authoring = the activation gate). Several → STATE your
selection rule in the card (the single director in the chosen SubScene; else
the one with timeline-reference authoring; else ask). Zero → protocol §6.

**3.4 Physics body by COMPONENT, never by name** (same bracket; the track binds
a **GameObject**):
```csharp
var bodyType = System.AppDomain.CurrentDomain.GetAssemblies()
    .Select(a => a.GetType("Unity.Physics.Authoring.PhysicsBodyAuthoring"))
    .FirstOrDefault(x => x != null);   // handles com.unity.physics vs .custom variants
var bodies = UnityEngine.Object.FindObjectsByType(bodyType,
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
// per body: hierarchy path, scene.path, MotionType, Mass, GravityFactor
```
ZERO bodies → a missing prerequisite ("no PhysicsBodyAuthoring in the SubScene — a
physics-stage specialist must add one; I override gravity, I don't create
bodies"). Several → confirm with the designer. **Path prediction (record it in
your card):** Dynamic + authored GravityFactor=1 → ADD path (one-step latency,
exact-archetype restore); GravityFactor≠1 or Kinematic → MUTATE path
(same-tick, in-place restore). Also check whether any timeline on this body
carries a `PhysicsKinematicOverrideTrack` — if so, surface the conflict matrix
(kinematic skill) before proceeding.

**3.5 PRE-capture (print + journal BEFORE any mutation):**
`PRE|playableAsset=<asset PATH or null>` (via `AssetDatabase.GetAssetPath`);
one `PRE|binding|<i>|<track name>|<track type>|<bound object hierarchy path +
type, or null>` line per `GetOutputTracks()` of the CURRENT asset via
`director.GetGenericBinding(track)`. Capture the asset PATH and each track's
NAME/index even when the table looks empty — they make the undo journal
replayable (UNDO-1 reloads the old asset by path and re-binds by name/index).

**Name resolution rule**: `GameObject.Find` misses inactive objects and is
ambiguous on duplicates — confirm the chosen name is active and unique in the
SubScene, else walk the SubScene roots to the recorded hierarchy path (or
`FindObjectsByType` filtered by `scene`).

## 4. CANONICAL RECIPES

One logical change per exec block; each block prints its `PRE|` capture before
mutating (protocol §2), saves inside the block, and is verified from a fresh
load (§7).

**4.1 Create timeline + track + clips, then wire the director:**

```csharp
// ---- parameters (discovered in §3 / chosen with designer) ----
var parentScenePath = "<DISCOVERED>";  var subScenePath = "<DISCOVERED>";   // §3.2
var directorGoName  = "<DISCOVERED>";  var bodyGoPath   = "<DISCOVERED>";   // §3.3 / §3.4
var assetFolder = "<CHOSEN>"; var assetPath = assetFolder + "/<Name>.playable";
var trackName = "<CHOSEN>";

var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    // CAPTURE (print + journal): PRE|folderExisted=<bool> PRE|assetExisted=<bool>
    var folderExisted = UnityEditor.AssetDatabase.IsValidFolder(assetFolder);
    if (!folderExisted) { /* CreateFolder for each missing segment */ }

    var timeline = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
    UnityEditor.AssetDatabase.CreateAsset(timeline, assetPath);
    var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideTrack>(null, trackName);

    // Pattern ZERO-G: weightless window, restore at timeline end
    var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideClip>();
    clipA.start = 0; clipA.duration = 2; clipA.displayName = "<clipName>";
    var a = (BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideClip)clipA.asset;
    a.gravityScale = 0f; a.restoreOnExit = true;

    // Pattern REVERSE + BLEND: overlap with the previous clip to glide between
    // regimes — overlaps genuinely lerp (this track blends; its Filter sibling races)
    var clipB = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideClip>();
    clipB.start = 1.5; clipB.duration = 2; clipB.displayName = "<clipName>";
    var b = (BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideClip)clipB.asset;
    b.gravityScale = -1f; b.restoreOnExit = true;
    clipB.blendInDuration = 0.5;   // mirrored computed blendOut on the earlier clip

    // Pattern PERMANENT (e.g. moon gravity 0.5): restoreOnExit=false on the LAST
    // clip — WARN the designer: permanent component + next-run capture poisoning
    // (trap in §2); pair with a compensating clip when it must be temporary.

    foreach (var o in new UnityEngine.Object[] { timeline, track, a, b })
        UnityEditor.EditorUtility.SetDirty(o);
    UnityEditor.AssetDatabase.SaveAssets();

    // Wire the director (binding table lives in the SCENE file -> persists fine).
    // CAPTURE (print + journal) BEFORE mutating: PRE|playableAsset, PRE|binding| lines (§3.5)
    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    var bodyGo = UnityEngine.GameObject.Find(bodyGoPath);   // see Name resolution rule
    director.playableAsset = timeline;
    director.SetGenericBinding(track, bodyGo);              // the GameObject ITSELF, not a component
    UnityEditor.EditorUtility.SetDirty(director);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "OK|" + assetPath;
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

Clip starts/durations/scales are example choices, not constants of the package.
Verify per §7 in SEPARATE blocks before claiming success.

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project: `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`).
  Parent scene `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`.
- Stage: `Stage_Director` (PlayableDirector + TimelineReferenceAuthoring, the
  only director). Body: **Stage_PhysicsBall** — dynamic sphere at (0,1,5),
  fresh-load dump `BODY|MotionType=Dynamic|Mass=1.000|GravityFactor=1.000`,
  `SHAPE|ShapeType=Sphere|ForceUnique=True`, plus LifeCycle + Targets.
  GravityFactor=1.000 ⇒ no `PhysicsGravityFactor` baked ⇒ every training clip
  exercised the ADD path.
- Asset built in training: `Assets/Training/16-physics-gravity-override-track/GravityOverrideMastery.playable`
  — one track `GravityTrack`, clips A_ZeroG (0–2s, gravityScale=0,
  restoreOnExit=true), B_ReverseG (1.5–3.5s, gravityScale=-1, restoreOnExit=true,
  blendIn 0.5 — the REAL-blending exhibit: at t=1.75 weights 0.5/0.5 →
  `math.lerp(0,-1,0.5) = -0.5`), C_PermanentMoonG (5–6s, gravityScale=0.5,
  restoreOnExit=false — the permanence/poisoning exhibit; the 3.5–5.0s gap holds
  B's −1).
- Wiring: binding = `GravityTrack → Stage_PhysicsBall (GameObject)`, the 14th
  table entry (B13 was lesson 15's FilterOverrideTrack → the same GameObject);
  director restored afterward to
  `Assets/Training/01-transform-position-track/PositionMastery.playable` (the
  binding entry SURVIVES the swap — keyed by track asset).
- Known pre-existing vex-ee console entries: UnityCliConnector HTTP server
  start, PerformanceTesting setup/cleanup, TestResults.xml save, lessons 08–10
  `[Worker2]` EntityLinks bake errors.

## 6. UNDO APPENDIX

This track's runtime effect (the `PhysicsGravityFactor` add/mutate, regime
state, capture poisoning) exists ONLY in play mode and is never serialized — the
journal covers the AUTHORING artifacts. The permanence/poisoning hazard is a
designer warning under Gaps, not a journal entry.

Artifact inventory for one run of §4 (vex-ee instance in §5):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip
   sub-assets — `DeleteAsset` removes all sub-assets with the file).
2. Possibly-created folder(s) `<assetFolder>` (only if `PRE|folderExisted=false`).
3. Mutated `director.playableAsset` (vex-ee: captured pre value
   `Assets/Training/01-transform-position-track/PositionMastery.playable`).
4. Added generic binding entry for the new track — persists even after
   `playableAsset` is swapped back (keyed by track asset); needs an explicit
   `ClearGenericBinding`. `EXPECTED:` the vex-ee report printed only
   `BINDINGS|count=13` pre-lesson — the 13 prior entries' contents were never
   individually captured (only B13's identity is known from lesson 15); capture
   full `PRE|binding|` lines yourself per §3.5.
5. No scene component values were changed (this recipe never touches the body's
   authoring components).

ORDER: restore the director FIRST so nothing in the scene references the asset,
THEN delete the asset, THEN restore any other captured scene values — deleting
the asset while the director still points at it would leave a dangling
`{fileID: 0}`-style reference instead of the captured pre-state.

Journal entry templates (protocol §5 — fill from YOUR captures, reverse order):

```csharp
// UNDO-1: restore director's captured playableAsset + binding table (SubScene bracket)
var parentScenePath = "<CAPTURED>"; var subScenePath = "<CAPTURED>";
var directorGoName = "<CAPTURED>"; var assetPath = "<CAPTURED>";
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    foreach (var tr in UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>(assetPath).GetOutputTracks())
        director.ClearGenericBinding(tr);   // entries I added for MY tracks
    // restore each CAPTURED PRE|binding| line: reload the PREVIOUS asset by captured
    // path, match tracks by name/index, re-find bound objects by captured hierarchy path.
    director.playableAsset =                // restore CAPTURED value, never "default"
        UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Playables.PlayableAsset>("<CAPTURED pre path>");
    UnityEditor.EditorUtility.SetDirty(director);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "UNDONE|director restored";
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

```csharp
// UNDO-2: delete the created .playable (+ folder, only if PRE|folderExisted=false and now empty)
var assetPath = "<CAPTURED>"; var assetFolder = "<CAPTURED>"; var folderExisted = false; // <CAPTURED>
var ok = UnityEditor.AssetDatabase.DeleteAsset(assetPath);
if (!folderExisted && UnityEditor.AssetDatabase.FindAssets("", new[]{ assetFolder }).Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset(assetFolder);
return "UNDONE|deleted=" + ok + "|" + assetPath;
```

```csharp
// UNDO-3: restore any other captured scene values — for THIS track family there are
// normally none beyond UNDO-1; include only entries your own journal recorded.
```

UNDO-4 (verification, fresh load — protocol §7): reload the SubScene additively;
print `director.playableAsset` (must equal the CAPTURED pre value) and the
binding table (must equal the captured `PRE|binding|` lines and NOT contain your
cleared track entry); confirm
`AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null`; restore
the parent scene; `unity-cli console --filter error` clean against the project
baseline.

## 7. VERIFICATION PROTOCOL

1. **Fresh-load asset dump** (new exec block): load `<assetPath>`, dump every
   track/clip (name, start/duration, blendIn/blendOut, caps,
   `gravityScale`/`restoreOnExit`); all clips `caps=Blending`. In-memory state
   after a save is not evidence.
2. **Raw YAML**: `gravityScale` plain floats, `restoreOnExit` as 1/0; REAL blend
   YAML on any overlap (`m_BlendOutDuration` earlier clip, `m_BlendInDuration`
   later, populated mix curves).
3. **Path prediction**: fresh-load dump of the bound body —
   MotionType/Mass/GravityFactor. Factor 1 + Dynamic ⇒ ADD path; ≠1 or
   Kinematic ⇒ MUTATE. Record which in your card.
4. **Binding table from a RELOADED SubScene**: expect
   `BINDING|<trackName>|bound=<bodyGoName> (UnityEngine.GameObject)`; all
   pre-existing entries intact (count = captured + your additions).
5. **Parent-scene restore**: `sceneCount=1`,
   `scene[0]=<parentScenePath>|loaded=True|active=True|dirty=False`.
6. **Console**: `unity-cli console --filter error` — nothing new beyond the
   project baseline (§5). This pipeline is silent even when misconfigured —
   silence is expected, not evidence (family pattern 7).
