---
name: unity-track-physics-filter-override
description: Master of PhysicsFilterOverrideTrack + PhysicsFilterOverrideClip (package BovineLabs.Timeline.Physics) — while-timeline-active collision-filter blob mutation, the ForceUnique requirement, the timeline-end-not-clip-end restore; carries the PHYSICS FAMILY SHARED PATTERNS reference. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "make this phase through walls / stop colliding with X during this clip".
---

# PhysicsFilterOverrideTrack specialist

## 1. SCOPE

You are the specialist for **`PhysicsFilterOverrideTrack`** and
**`PhysicsFilterOverrideClip`** from the package `BovineLabs.Timeline.Physics`
(namespace `BovineLabs.Timeline.Physics.Authoring.Filters`). Scope: exactly this
track family — authoring the track/clips in a `.playable` TimelineAsset, wiring a
SubScene PlayableDirector (GameObject binding), the ForceUnique prerequisite, and
the runtime filter-override semantics. Physics bodies/stages belong to a
stage/physics-setup specialist — a missing physics body is a missing-prerequisite report,
never something you create. Gravity → `unity-track-physics-gravity-override`;
kinematic freezing → `unity-track-physics-kinematic-override`. **This skill also
carries the PHYSICS FAMILY SHARED PATTERNS reference** (§2) that sibling physics
skills cite instead of re-deriving.
Behave per unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say
where a fact was PROVEN, not where it applies. (All verified vex-ee 2026-06 via
reflection dumps, package-source reads, raw YAML reads, fresh-load read-backs
through `unity-cli exec`; no play mode — runtime claims are source-derived.)

| Type | Facts |
|---|---|
| `PhysicsFilterOverrideTrack` | `BovineLabs.Timeline.Physics.Authoring.Filters`, asm `BovineLabs.Timeline.Physics.Authoring`, sealed, EMPTY body, base `DOTSTrack`. `[TrackClipType(PhysicsFilterOverrideClip)]`, `[TrackBindingType(typeof(GameObject))]` (GameObject, not a component), `[TrackColor(0.8,0.2,0.2)]`, `[DisplayName("BovineLabs/Physics/Filter Override")]`. |
| `PhysicsFilterOverrideClip` | sealed, base `DOTSClip`, `ITimelineClipAsset`, `clipCaps => ClipCaps.None`, `duration => 1` (seed only). |
| `PhysicsFilterOverrideData` | `...Physics.Data`, IComponentData: `uint BelongsToOverride; uint CollidesWithOverride; bool RestoreOnExit;` |
| `PhysicsFilterOverrideAnimated` | `IAnimatedComponent<PhysicsFilterOverrideData>` — `AuthoredData` + `Value`, on the CLIP entity (plumbing only; nothing animates). |
| `ActiveFilterOverride` | IComponentData + **IEnableableComponent**: `PhysicsFilterOverrideData Config` — on the BINDING entity, added DISABLED at bake. |
| `PhysicsFilterOverrideState` | IComponentData: `bool Fired; uint OriginalBelongsTo; uint OriginalCollidesWith;` — on the BINDING entity. |
| Systems | `PhysicsFilterOverrideTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`, per rendered frame) produces the enabled `ActiveFilterOverride{Config}`; `PhysicsFilterOverrideApplySystem` (`PhysicsModifierGroup` = FixedStep, after `PhysicsSystemGroup`; query uses `IgnoreComponentEnabledState`) consumes it against the collider blob. |

Clip fields (camelCase, reflection on a fresh instance): `belongsToOverride`
uint, default `4294967295` (0xFFFFFFFF) — new BelongsTo bitmask, raw;
`collidesWithOverride` uint, default `4294967295` — new CollidesWith bitmask,
raw; `restoreOnExit` bool, default `True` — restore captured masks when the
override REGIME ends (timeline deactivation, NOT clip end). YAML (raw-read
verified): uints as decimal, bools as 1/0; no blend/ease YAML — ClipCaps.None.

Bake (source-read from `PhysicsFilterOverrideClip.Bake`): unconditional — no
guard, no LogError; adds exactly `PhysicsFilterOverrideAnimated{AuthoredData}`
to the clip entity, then `base.Bake`. **NO bake-time failure mode.** The
binding-entity pair comes from the central `PhysicsTimelineBakingSystem`: it
queries `TrackBinding` + `PhysicsFilterOverrideAnimated` (incl. disabled +
prefab entities), does `if (target == Entity.Null) continue;` — unbound track =
total silent no-op — and gives the target `ActiveFilterOverride` (added then
`SetComponentEnabled(false)`) + `PhysicsFilterOverrideState`, only if absent.

### Runtime semantics (source-quoted)

The track system runs the family kernel per rendered frame (family patterns
4–5 below; `ResetStateTrackJob` resets State to `{Fired=false}` on
clip-activation edges only while `ActiveFilterOverride` is disabled; no
`ClipWeight` is ever baked — first writer wins). `PhysicsFilterOverrideApplySystem`
then runs the Fired machine per fixed step against the collider BLOB: **enter**
captures `ptr->GetCollisionFilter()` into State and writes the overrides via
`ptr->SetCollisionFilter(newFilter)` — direct unsafe in-place blob mutation,
GroupIndex untouched; **stay** re-applies the masks every fixed step; **exit**
restores the captured masks `if (config.RestoreOnExit)` and clears Fired — and
`!isActive` only ever becomes true at timeline deactivation, using the
LAST-written Config. Guards: invalid collider → silent skip; shared blob →
`[BurstDiscard]` LogWarning + skip (silent in a Bursted player). The family's
only track (verified through topic 21) writing inside a BlobAsset.

**Mental model:** NOT per-clip while-active — the regime runs from the first
filter clip's first frame to timeline deactivation; gaps hold the last Config;
exit runs ONCE and the LAST clip's `restoreOnExit` decides for everyone.
(Concrete walk in §5.)

### Traps & DO/DON'T (each proven live or source-quoted, vex-ee 2026-06)

- **DON'T expect the override on a default-baked collider — baked blobs are
  SHARED by default; the IsUnique guard skips them.** `Collider.IsUnique =>
  m_Header.ForceUniqueBlobID != k_SharedBlobID (0u)`; bake sets
  `ForceUniqueIdentifier = isUnique ? shape.ForceUniqueID : 0u`, `isUnique =
  isForceUniqueComponentPresent || shape.ForceUnique` — only the ForceUnique
  checkbox (or `ForceUniqueColliderAuthoring`) makes a baked blob unique;
  runtime-created colliders default unique. Exact warning, quoted from
  `PhysicsFilterOverrideApplySystem.ApplyJob`: `"PhysicsFilterOverride targets a
  shared collider blob; the override was skipped. Enable 'Force Unique' on the
  bound body's collider authoring so the filter can be modified per instance."`
  Runtime alternative: `EnsureUniqueColliderBlobTag` →
  `EnsureUniqueColliderSystem.MakeUnique`. And **the warning is
  `[BurstDiscard]`** — editor/mono only; in a fully Bursted player the
  shared-blob skip is SILENT, so a clean player log proves nothing.
- **DON'T expect restore at clip end** — `DisableStaleTrackJob` is the only
  mid-pipeline disabler and it keys off `TimelineActive`, not `ClipActive`; gaps
  hold the last Config; the LAST clip's `restoreOnExit` decides the whole run.
- **DON'T let a restoreOnExit=false clip near a collider you ever want back —
  capture poisoning across runs.** Within one run the hazard cannot occur (Fired
  stays true; `ResetStateTrackJob` is blocked while Active is enabled, so
  `State.Original*` keeps the true pre-run masks); but run 1 ending on a
  permanent clip leaves mutated masks with Fired=false, and run 2's first enter
  captures them as the new "original" — even restore=true clips now "restore"
  the ghost. No log; no undo except a compensating override or external
  `SetCollisionFilter`.
- **DON'T overlap two filter clips on one binding — FIRST-writer-wins, not
  blending.** `ClipBaker.AddClipBaseComponents` adds `ClipWeight` only when caps
  include Blending; under ClipCaps.None all filter clips take `blendData.TryAdd`
  (silent fail on existing key) — the first-processed clip's whole config wins,
  "first" = chunk/entity iteration order. The `DiscreteMixer{ Lerp(a,b,s) =>
  s >= 0.5 ? b : a; Add(a,b) => b; }` path is dead code here. Applies across
  tracks too (map keyed by binding entity). Stagger filter clips.
- **DO understand why stay re-applies every fixed step** — other systems can
  rebuild/overwrite the collider or filter; re-asserting wins the frame and
  makes scrubbing harmless while active. The mutation is invisible to
  change-version filters (the blob reference never changes) and nothing else
  snapshots it — exactly why the IsUnique guard exists: a shared-blob write
  would change every entity using it, prefab sources included.
- **DO remember GroupIndex passes through untouched** — all branches replace
  only BelongsTo/CollidesWith and State stores only those two; this track cannot
  express "join/leave collision group N", and a nonzero GroupIndex keeps
  overriding mask decisions during AND after the clip.
- **DO bind the GameObject itself** — `GetGenericBinding` returns it verbatim
  (never coerces, unity-cli rule 5k); the PlayableDirectorBaker coerces
  GameObject→entity at bake. **Mutual visibility caveat:** collision requires
  both bodies' filters to agree (`a.BelongsTo & b.CollidesWith` and vice versa);
  overriding one body can't force a collision the other body's filter rejects.

### PHYSICS FAMILY SHARED PATTERNS (canonical reference for the physics-track family)

Verified from package source (`Packages/BovineLabs.Timeline.Physics/`), 2026-06.

**1. Two-system split.** Every physics track = `<X>TrackSystem` in
`TimelineComponentAnimationGroup` (per rendered frame,
`[UpdateAfter(EntityLinkTargetPatchSystem)]`) producing an enabled
`Active<X>{Config}` on the binding entity, + `<X>ApplySystem` in a fixed-step
group consuming it — produced per frame, applied per fixed step (0+/frame).

**2. Producer vs modifier groups — which side of the physics step.** Both in
`FixedStepSimulationSystemGroup`: `PhysicsProducerGroup` is
`[UpdateBefore(PhysicsSystemGroup)]`, `PhysicsModifierGroup` is
`[UpdateAfter(PhysicsSystemGroup)]` (quoted). FEEDERS in producer: PID
(linear+angular), Ricochet, GravityOverride, Kinematics,
TriggerQuery/Condition/Force/Instantiate, SocketReturn, ChainFollow. CORRECTORS
in modifier: **FilterOverride**, Drag, KinematicOverride, Teleport,
VelocityClamp, VelocityOverride, ChainGrab/Reel/Release. The force accumulator
deliberately exists in BOTH.

**3. The Active*+State pair on the BINDING entity, added at bake.** One central
`PhysicsTimelineBakingSystem` (BakingSystem world) walks every clip entity with
`<X>Animated` + `TrackBinding` and gives the TARGET: `Active<X>`
(IEnableableComponent, added DISABLED) + `<X>State`. Holds for LinearPID,
AngularPID, Force (+`PhysicsForceRandom`), Velocity, Ricochet, FilterOverride,
GravityOverride, VelocityClamp, KinematicOverride; exception: Drag gets
`ActiveDrag` only — no State. Force/Velocity/PID/Drag targets also get
`PendingForce` + `PendingVelocity` buffers + `PendingVelocityReset` (disabled) —
those tracks write pending buffers an accumulator drains around the step.

**4. Per-track overlap rules (SharedTrackJobs.cs / TrackBlendDriver kernel).**
`TrackBlendImpl` keeps a per-binding-entity hash map; clips WITHOUT `ClipWeight`
(ClipCaps.None tracks) use `TryAdd` = first-writer-wins; clips WITH weights use
the 4-slot weight-sorted register (strict `>` insertion; full-weight tie
normalizes to s=0.5 and the mixer picks the later-accumulated).
`TrackBlendDriver<TData,TAnimated,TActive,TMixer>` packages the kernel job
sequence; FilterOverride hand-rolls the same sequence.

**5. The Fired state machine — the family's real "while-active" unit.** Enter
(active && !Fired): capture originals into State, apply, Fired=true. Stay:
re-apply every fixed step (scrub-safe, fights concurrent writers). Exit
(!active && Fired): restore from State if configured, Fired=false. Nothing
disables Active between clips ⇒ **the restore unit is the whole timeline
activation, not the clip**: first clip enters the regime, later clips only
retarget Config, gaps keep the last Config applied, exit runs once at timeline
deactivation with the LAST clip's Config (incl. its restore flag). Same shape in
every capture/restore physics track (KinematicOverride, GravityOverride,
VelocityClamp...). Apply queries use `IgnoreComponentEnabledState` and read the
enabled bit per entity — what lets the exit see "just disabled" entities.

**6. The IsUnique blob rule** (every collider-blob-mutating track): full chain
in the first trap above. Designer checklist: tick ForceUnique on the bound
body's shape.

**7. Silence profile (family triage rule).** Bake: totally silent — no guards
exist. Runtime: silent skips for null/invalid colliders and unbound tracks; the
ONE loud failure is the shared-blob warning, only in managed builds. A clean
console proves nothing except (in editor) that the blob was unique.

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem;
never play mode. unity-cli Safe Loop on every mutation. Names below are
parameters — discover them in THIS project; never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideTrack, BovineLabs.Timeline.Physics.Authoring");
return t == null
    ? "MISSING_PREREQUISITE|PhysicsFilterOverrideTrack not found - package BovineLabs.Timeline.Physics is absent in this project"
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
    .FirstOrDefault(x => x != null);   // com.unity.physics vs .custom variants
var bodies = UnityEngine.Object.FindObjectsByType(bodyType,
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
// per body: hierarchy path, scene.path, MotionType, Mass; from its PhysicsShapeAuthoring:
// ShapeType + ForceUnique (property, or SerializedObject "m_ForceUnique")
```
ZERO bodies → a missing prerequisite ("no PhysicsBodyAuthoring in the SubScene — a
physics-stage specialist must add one; I override filters, I don't create
bodies"). Several → confirm with the designer. **Prerequisite (this track's
readiness test): `ForceUnique` on the chosen body's shape must be true**, or
the override warn-and-skips at runtime (silently in Bursted players). If false:
report as a Gap, or — only with designer approval (it changes the body's bake)
— flip it per recipe 4.1 with PRE capture and a journal entry.

**3.5 PRE-capture (print + journal BEFORE any mutation):**
`PRE|playableAsset=<asset PATH or null>` (via `AssetDatabase.GetAssetPath`);
one `PRE|binding|<i>|<track name>|<track type>|<bound object hierarchy path +
type, or null>` line per `GetOutputTracks()` of the CURRENT asset via
`director.GetGenericBinding(track)`; `PRE|ForceUnique=<bool>` (only if 4.1 will
run). Capture the asset PATH and each track's NAME/index even when the table
looks empty — they make the undo journal replayable (UNDO-1 reloads the old
asset by path and re-binds by matching name/index).

**Name resolution rule**: `GameObject.Find` misses inactive objects and is
ambiguous on duplicates — confirm the chosen name is active and unique in the
SubScene, else walk the SubScene roots to the recorded hierarchy path (or
`FindObjectsByType` filtered by `scene`).

## 4. CANONICAL RECIPES

One logical change per exec block; each block prints its `PRE|` capture before
mutating (protocol §2), saves inside the block, and is verified from a fresh
load (§7).

**4.1 (conditional) Enable ForceUnique on the discovered shape** — only when
§3.4 found it false AND the designer approved. Inside the SubScene bracket (as
in 4.2): print + journal `PRE|ForceUnique=<shape.ForceUnique>`; then
`shape.ForceUnique = true;` (or SerializedObject `m_ForceUnique` +
ApplyModifiedProperties); `SetDirty(shape)`; `SaveScene(subScene)`. Fresh-load
verify per §7.

**4.2 Create timeline + track + clips, then wire the director:**

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
    var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideTrack>(null, trackName);

    // Pattern GHOST: phase through everything, restore at timeline end
    var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideClip>();
    clipA.start = 0; clipA.duration = 2; clipA.displayName = "<clipName>";
    var a = (BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideClip)clipA.asset;
    a.belongsToOverride = 0; a.collidesWithOverride = 0; a.restoreOnExit = true;

    // Pattern SELECTIVE: collidesWithOverride = 1u<<N (raw uint masks); leave
    // belongsToOverride 0xFFFFFFFF only if "everything" is acceptable for what
    // OTHERS see of this body; both masks always write. NEVER overlap filter
    // clips (first-writer-wins race) — stagger them.
    // Pattern PERMANENT: restoreOnExit=false on the LAST clip — WARN the designer:
    // it also poisons every later capture on this collider (trap in §2).

    foreach (var o in new UnityEngine.Object[] { timeline, track, a })
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

Clip starts/durations/masks are example choices, not constants of the package.
Verify per §7 in SEPARATE blocks before claiming success.

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project: `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`).
  Parent scene `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`.
- Stage (built by unity-stage-foundations): `Stage_Director` (PlayableDirector +
  TimelineReferenceAuthoring, the only director). Body: **Stage_PhysicsBall** —
  dynamic sphere at (0,1,5), PhysicsBodyAuthoring (Dynamic, Mass=1) +
  PhysicsShapeAuthoring (sphere r=0.5) + LifeCycle + Targets (Target=Stage_Target).
  **ForceUnique history:** the lesson-15 dump read `m_ForceUnique=False` (would
  warn-and-skip at runtime); corrected to `True` AFTER lesson 15 by the stage
  owner, outside the trainee's journaled run — re-verify live. Stage_TriggerZone
  also exists; NOT this track's binding target.
- Asset built in training: `Assets/Training/15-physics-filter-override-track/FilterOverrideMastery.playable`
  — one track `FilterOverrideTrack`, clips A_Ghost (0–2s, belongsTo=0,
  collidesWith=0, restoreOnExit=true), B_OnlyLayer1 (3–5s, collidesWith=2,
  belongsTo left 0xFFFFFFFF, restoreOnExit=true), C_PermanentGhost (6–7s, both
  0, restoreOnExit=false — the permanence/poisoning exhibit).
- Regime walk: ghost at t=0, STAYS ghost through the 2–3s gap, B's masks 3–6s,
  ghost again 6–7s; at timeline end the exit runs once with C's
  `RestoreOnExit=false` → permanently ghost.
- Wiring: binding = `FilterOverrideTrack → Stage_PhysicsBall (UnityEngine.GameObject)`,
  the 13th table entry; director restored afterward to
  `Assets/Training/01-transform-position-track/PositionMastery.playable` (the
  binding entry SURVIVES the swap — keyed by track asset).
- Known pre-existing vex-ee console entries: UnityCliConnector HTTP server
  start, PerformanceTesting setup/cleanup, TestResults.xml save, lessons 08–10
  `[Worker2]` EntityLinks bake errors.

## 6. UNDO APPENDIX

This track's runtime effect (blob mask mutation, regime state, capture
poisoning) exists ONLY in play mode and is never serialized — the journal covers
the AUTHORING artifacts. The poisoning hazard is a designer warning under Gaps,
not a journal entry.

Artifact inventory for one run of §4 (vex-ee instance in §5):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip
   sub-assets — `DeleteAsset` removes all sub-assets with the file).
2. Possibly-created folder(s) `<assetFolder>` (only if `PRE|folderExisted=false`).
3. Mutated `director.playableAsset` (vex-ee: captured pre value
   `Assets/Training/01-transform-position-track/PositionMastery.playable`).
4. Added generic binding entry for the new track — persists even after
   `playableAsset` is swapped back (keyed by track asset); needs an explicit
   `ClearGenericBinding`. `EXPECTED:` the vex-ee report printed only
   `BINDINGS|count=12` pre-lesson — the 12 prior entries' contents were never
   individually captured; capture full `PRE|binding|` lines yourself per §3.5.
5. If recipe 4.1 ran: mutated `ForceUnique` on the body's shape (restore the
   CAPTURED bool). `EXPECTED:` in vex-ee the True flip happened outside the
   journaled lesson run — no PRE capture of it exists in the report (the
   lesson-time value was False).

ORDER: restore the director FIRST so nothing in the scene references the asset,
THEN delete the asset, THEN restore other captured scene values (ForceUnique) —
deleting the asset while the director still points at it would leave a dangling
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
// UNDO-3: restore captured ForceUnique (ONLY if your journal recorded recipe 4.1):
// SubScene bracket; find the shape by captured hierarchy path;
// shape.ForceUnique = <CAPTURED PRE value>; SetDirty; SaveScene; restore parent.
```

UNDO-4 (verification, fresh load — protocol §7): reload the SubScene additively;
print `director.playableAsset` (must equal the CAPTURED pre value), the binding
table (must equal the captured `PRE|binding|` lines and NOT contain your cleared
track entry), and the shape's `ForceUnique` (captured value, if UNDO-3 ran);
confirm `AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null`;
restore the parent scene; `unity-cli console --filter error` clean against the
project baseline.

## 7. VERIFICATION PROTOCOL

1. **Fresh-load asset dump** (new exec block): load `<assetPath>`, dump every
   track/clip (name, start/duration, caps, `belongsToOverride`/
   `collidesWithOverride`/`restoreOnExit`); all clips `caps=None`. In-memory
   state after a save is not evidence.
2. **Raw YAML**: uints as decimal, bools as 1/0; NO blend/ease YAML
   (ClipCaps.None, clips never overlapped).
3. **Prerequisite**: fresh-load dump of the bound body — MotionType/Mass/
   ShapeType and **`m_ForceUnique=True`** (False ⇒ warn-and-skip at runtime;
   silent in Bursted players).
4. **Binding table from a RELOADED SubScene**: expect
   `BINDING|<trackName>|bound=<bodyGoName> (UnityEngine.GameObject)`; all
   pre-existing entries intact (count = captured + your additions).
5. **Parent-scene restore**: `sceneCount=1`,
   `scene[0]=<parentScenePath>|loaded=True|active=True|dirty=False`.
6. **Console**: `unity-cli console --filter error` — nothing new beyond the
   project baseline (§5). This pipeline is bake-silent even when misconfigured —
   silence is expected, not evidence (family pattern 7).
