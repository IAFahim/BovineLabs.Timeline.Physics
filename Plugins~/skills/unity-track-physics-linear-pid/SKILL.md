---
name: unity-track-physics-linear-pid
description: Master of PhysicsLinearPIDTrack + PhysicsLinearPIDClip (package BovineLabs.Timeline.Physics) — a physical position motor via PendingForce with five target modes, the InitialLocal per-activation capture trap, offset-dependent fallback self-chase. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "make this body physically fly to / hover at / follow / flee a point".
---

# PhysicsLinearPIDTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsLinearPIDTrack`** and **`PhysicsLinearPIDClip`** from the
package `BovineLabs.Timeline.Physics`, ns `BovineLabs.Timeline.Physics.Authoring.PIDs` — bound to
a **`PhysicsBodyAuthoring` COMPONENT**. Scope: authoring track/clips in a `.playable`, wiring a
SubScene PlayableDirector, the runtime position-motor semantics. Physics bodies, Targets slots, and
stat setups are OTHER specialists' domains (protocol §6: report a missing prerequisite, never improvise).
**The SHARED PID CORE lives in `unity-track-physics-angular-pid`** (PidTuning, the
`ComputePidForce` anti-windup kernel, `PidStateData` lifecycle, `PendingForce` drain,
`StatStrengthUtility.Resolve` triple trap, tuning doctrine + presets); family patterns in
`unity-track-physics-filter-override` — cite both, don't re-derive. **This skill owns the
FIVE-MODE MATRIX**, the InitialLocal capture trap, and the Linear-vs-Angular deltas.
Behave per unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say where a fact was
PROVEN, not where it applies. (All verified vex-ee 2026-06 via reflection dumps, package-source +
raw YAML reads, fresh-load read-backs, **direct numeric invocation of the shipped kernel/mixer**
through `unity-cli exec`; no play mode.)

While a clip is active, a PID controller computes `error = targetPos − selfPos` (plain float3
subtraction, UNBOUNDED magnitude) toward a goal derived per `PidLinearTargetMode` and appends
`force × dt` into **`PendingForce.Linear`**, drained the SAME tick through **InverseMass** into
`PhysicsVelocity.Linear` (Mass=1 → force ≈ accel) — a real physical motor, never a transform
write. No capture/restore: at timeline end the controller is disabled and the body keeps its
momentum. Runtime effects exist only in play mode.

| Aspect | Linear (this skill) | Angular (banked, angular-pid skill) |
|---|---|---|
| Error space | `targetPos − selfPos`, plain float3, UNBOUNDED | shortest-path axis-angle, \|e\| ≤ π |
| Goal field | `float3 TargetOffset` (offset OR absolute world pos) | `quaternion TargetRotation` |
| Mode set | TargetLocal / InitialLocal / LineOfSight / World / FleeFromTarget | MatchTarget / LookAtTarget / World / FleeFromTarget / MatchTargetOpposite |
| Snapshot mode | **InitialLocal uses `CapturedTargetPosition`** | none — that field is unused plumbing |
| Output + drain | `PendingForce{Linear = force×dt}` → world impulse × **InverseMass** → `PhysicsVelocity.Linear` | `PendingForce{Angular = torque×dt}` → world→body-local × **InverseInertia** → `.Angular` |
| Mixer Add | **dominant config by higher Strength carries enums** (tie → lower mode byte); offsets/gains summed | gains summed; rotation via quaternion log/exp sum |
| Saturation | error grows without bound → P easily saturates MaxOutput (Flee guarantees it) | P tops out at P·π (~31 at default) |

| Type | Facts |
|---|---|
| `PhysicsLinearPIDTrack` | ns `...Physics.Authoring.PIDs`, asm `...Physics.Authoring`, base `DOTSTrack`, EMPTY body. `[TrackClipType(typeof(PhysicsLinearPIDClip))]`, `[TrackColor(0.9,0.2,0.4)]`, **`[TrackBindingType(typeof(Unity.Physics.Authoring.PhysicsBodyAuthoring))]`**, `[DisplayName("BovineLabs/Physics/Linear PID")]`. |
| `PhysicsLinearPIDClip` | base `DOTSClip`, `ITimelineClipAsset`, **`clipCaps => ClipCaps.Blending \| ClipCaps.Looping`**, `duration => 1` (seed only). Bake: unconditional, SILENT — `PhysicsLinearPIDBuilder.ApplyTo`; null `readStatLink` → LinkKey 0; null `strengthStat` → default StatKey (`IsEnabled()==false` → multiplier 1). |
| `PhysicsLinearPIDData` | `PidTuning Tuning; Target TrackingTarget; PidLinearTargetMode TargetMode; float3 TargetOffset; float Strength; StatStrengthConfig StrengthStat;` — TargetOffset doc: "In World mode, this acts as the absolute world position. In Offset mode, it is an offset from the tracking target." `PhysicsLinearPIDAnimated` = `IAnimatedComponent<PhysicsLinearPIDData>` (`AuthoredData` + `Value`, CLIP entity). |
| `ActiveLinearPid` | IComponentData + **IEnableableComponent**: `PhysicsLinearPIDData Config` — BINDING entity, added DISABLED at bake. **Lowercase "Pid"** (confirmed live: `BovineLabs.Timeline.Physics.ActiveLinearPid`) — fully-qualify in exec snippets. `PhysicsLinearPIDState` = `PidStateData State` on the binding; comment on `CapturedTargetPosition`: "InitialLocal mode: locked on first tick". |
| Systems | `PhysicsLinearPIDTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`; the ANGULAR track system is `[UpdateAfter(...)]` THIS one — Linear runs first); shared **`PhysicsPidApplySystem`** (`PhysicsProducerGroup`, after KinematicsApply, BEFORE the step) hosts `AppendLinearJob`; `PhysicsProducerForceAccumulatorSystem` drains SAME tick. |

### Clip fields — camelCase serialized names, defaults from a fresh instance (reflection)

| Field | Type | Default | Meaning |
|---|---|---|---|
| `uniformAxes` | bool | `True` | EDITOR-ONLY sugar: one float drives X=Y=Z per gain. Not baked. |
| `tuning` | PidTuning | P=(10,10,10) D=(1,1,1) I=(2,2,2) Max=100 | Same non-preset default as Angular — integral-heavy/underdamped vs "Balanced". |
| `trackingTarget` | Target | `Target(1)` | Which `Targets` slot ON THE BODY names the target entity. |
| `targetMode` | PidLinearTargetMode | `TargetLocal(0)` | Goal derivation (matrix below). |
| `targetOffset` | Vector3 | `(0,0,0)` | Target-frame offset / sight-line offset / **absolute world goal in World mode**. |
| `strength` | float | `1` `[Min(0)]` | "Output force multiplier. 0 = no effect, 1 = full, 2 = double." |
| `strengthStat` / `readStatFrom` / `readStatLink` | StatSchemaObject / Target / EntityLinkSchema | `null` / `Self(4)` / `null` | Optional ×100-fixed-point stat MULTIPLIER; whose stat buffer (via the BODY's Targets); optional link override for the stat-entity hunt. |

Enum (live `Enum.GetValues` — re-dump in YOUR project): `PidLinearTargetMode : byte` =
**`TargetLocal=0, InitialLocal=1, LineOfSight=2, World=3, FleeFromTarget=4`**. YAML: enums as
ints, `targetOffset` as a Vector3 block, `strengthStat: {fileID: 0}` when null,
`m_BlendInDuration: -1` when no overlap authored.

### THE FIVE-MODE MATRIX (this skill's headline) — `PhysicsMath.ResolveLinearPidTarget`, quoted: unresolvable/missing target → `targetPos = selfPos; targetRot = selfRot;` (THE FALLBACK, silent), then

```csharp
targetPosition = config.TargetMode switch {
    TargetLocal or InitialLocal => targetPos + math.rotate(targetRot, config.TargetOffset),
    LineOfSight => ResolveLineOfSight(selfPos, targetPos, selfRot, config.TargetOffset),
    World => config.TargetOffset,  FleeFromTarget => selfPos + (selfPos - targetPos),
    _ => selfPos };                                    // unknown mode => zero error
```

`ResolveLineOfSight`: `dir = lengthsq(target−self) > 1e-5 ? normalize(diff) : mul(selfRot,
forward())`; `rot = LookRotationSafe(dir, up())`; goal = `targetPos + rotate(rot, offset)` — the
offset hangs off the TARGET along the sight line (offset.z negative stops short: "keep distance").

| Mode (byte) | Goal (target resolved) | Fallback verdict (target := SELF) |
|---|---|---|
| `TargetLocal=0` | `targetPos + rotate(targetRot, offset)` — moves WITH the target, offset in the TARGET's rotating frame | offset=0 → true no-op; **offset≠0 → chases its own offset FOREVER** (constant self-relative error re-derived each tick — linear twin of Angular's MatchTarget+offset trap). |
| `InitialLocal=1` | same formula, captured ONCE into `State.CapturedTargetPosition` on the first uninitialized tick; frozen thereafter | **Captures SELF(+offset) as the frozen goal** — offset=0 settles in place; offset≠0 converges to a frozen point near where it stood. Least-bad target-relative fallback (FREEZES instead of chasing). |
| `LineOfSight=2` | `targetPos + rotate(LookRotationSafe(normalize(target−self), up), offset)` | diff=0 → dir = self forward; offset=0 → no-op; **offset≠0 → perpetual chase in its own forward-aligned frame** (yaw/pitch only — roll discarded by `LookRotationSafe`). |
| `World=3` | `config.TargetOffset` **IS the absolute world goal** (field doubles as the position — name clips accordingly) | **Fallback-blind / immune** — targetPos/targetRot unused. Pair with `trackingTarget=None` (short-circuits before the Targets lookup). |
| `FleeFromTarget=4` | `selfPos + (selfPos − targetPos)` — reflected point, recomputed EVERY tick → goal recedes → **never converges, by design** | `self + (self−self) = self` → error 0 → **true no-op** — the only target-relative mode safely inert on a lost target regardless of offset (offset IGNORED by this branch entirely). |

The fallback substitutes self BEFORE the offset math: any nonzero offset turns a lost target into
a perpetual self-chase force — silently. Triage "body drifts during a PID clip" as a lost slot.

### THE INITIALLOCAL TRAP (per-activation capture, not per-clip)

Capture site, quoted from `PhysicsPidApplySystem.AppendLinearJob`:

```csharp
if (config.TargetMode == PidLinearTargetMode.InitialLocal) {
    if (!state.State.IsInitialized) state.State.CapturedTargetPosition = resolvedTargetPos; // capture ONCE
    targetPos = state.State.CapturedTargetPosition;               // frozen goal
}
// ... nextState.CapturedTargetPosition = capturedPos;  // re-threaded every tick
```

The gate is **`IsInitialized`** — set `true` by `ComputePidForce` on the first tick of ANY mode.
The only reset is `ResetStateTrackJob`: it fires on clip-activation edges but is **gated on
`ActiveLinearPid` being DISABLED** — within one timeline activation the Active bit stays enabled
from the first PID clip's first frame until timeline end. Consequences:

1. **InitialLocal as the FIRST PID clip of an activation**: reset ran on its edge (Active still
   disabled — WriteActiveJob's enable is a BeginSimulation ECB, one frame later) →
   `IsInitialized=false` → first apply tick captures → frozen for the clip. **Works as advertised.**
2. **InitialLocal AFTER another PID clip in the same activation** (the worked asset's clip C, §5):
   activation edge finds Active ENABLED → reset blocked → `IsInitialized` true since the first
   clip's first tick → **capture NEVER happens** → the body seeks the stale
   `CapturedTargetPosition` = **`float3.zero` — WORLD ORIGIN**, silently (an earlier InitialLocal
   capture this activation gets reused instead). True mid-timeline snapshot? Give the clip its own
   activation — **snapshots go FIRST**.
3. **Exit/re-run**: timeline end disables Active; State sits STALE (capture included). The NEXT
   activation's first clip edge finds Active disabled → reset → **re-runs re-capture**; State
   zeroes lazily then, never at exit.
4. **Blend-snap interaction**: TargetMode snaps at s=0.5 in the mixer; a mid-overlap snap INTO
   InitialLocal cannot capture (IsInitialized long true) → the goal teleports to the stale
   snapshot at the blend midpoint.

### Edge cases & traps (each source-proven or numerically confirmed, vex-ee 2026-06)

- **DON'T read fallback-to-self as one behavior — it is MODE- AND OFFSET-DEPENDENT** (matrix
  above): World immune, Flee true no-op, TargetLocal/LineOfSight perpetual self-chase at offset≠0,
  InitialLocal freezes self+offset — all silent, no log.
- **DO expect REAL blending — the Linear Add rule differs from Angular** — `PhysicsLinearPIDMixer`,
  quoted + numerically confirmed: `Lerp` lerps Tuning/TargetOffset/Strength component-wise;
  TrackingTarget/TargetMode/StrengthStat snap `s < 0.5f ? a : b` (at exactly 0.5, **b wins** —
  confirmed s=0.49/0.50/0.51); `Add` elects a **dominant** config by higher Strength (tie → lower
  TargetMode byte) whose enums/StrengthStat carry, while offsets/gains/Strength are SUMMED
  (Angular's Add sums rotation via quaternion log/exp).
- **DON'T blend two MODES and expect a smooth goal** — the goal formula switches species at s=0.5:
  the goal TELEPORTS at the midpoint (D-term spikes one tick); same-mode overlaps blend smoothly.
- **DON'T expect Flee to converge** — goal `self+(self−target)` recomputed per tick; error =
  distance-to-target, GROWS as it flees → P saturates MaxOutput → constant max force until
  drag/collision stops it — pair with VelocityClamp for a terminal speed.
- **DON'T trust the stat multiplier without walking the THREE-layer trap** (banked in the angular
  skill, applies verbatim): no Stat buffer → silently ×1; buffer present → ×100-decoded (authored
  25 = ×0.25); buffer present but KEY absent → `GetValueFloat` 0 → PID silently dead.
- **DO trust the kernel numerics — invoked directly**: first tick error=(1,0,0) dt=0.02 default
  tuning → out=(10.04,0,0), prevErr=error (ZERO D-kick); error=(100,0,0) → out clamped to len=100
  (MaxOutput magnitude clamp); I=0 → IntegralAccumulator hard-zeroed (`math.all(I≤0)` pure PD);
  dt=0 → out=(0,0,0), state untouched; CapturedTargetPosition=(7,8,9) survives the kernel unchanged.
- **DON'T treat gaps or exit as neutral** — family pattern 5: between clips the last Config keeps
  seeking; at timeline end `DisableStaleTrackJob` merely disables the bit — **momentum survives**
  (no restore path, no State.Original).
- **DO know tiny outputs vanish** (`lengthsq(force) > 1e-5f` micro-force skip: a near-settled body
  appends nothing) and **the silence profile holds** (family rule 7: no guard or LogError in Bake;
  unbound track → central-baker continue — clean console proves nothing).
- **Tuning**: shared presets + P → D → I doctrine in the angular skill. Default clip tuning (P10
  D1 I2) is integral-heavy/underdamped vs Balanced — expect overshoot; for crisp arrivals start
  from Balanced (10/3/1/100). A clip ending leaves linear momentum — VelocityClamp or accept it.

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never play mode;
unity-cli Safe Loop on every mutation. Names below are parameters — discover them in THIS project;
never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsLinearPIDTrack, BovineLabs.Timeline.Physics.Authoring");
return t == null ? "MISSING_PREREQUISITE|PhysicsLinearPIDTrack not found - package BovineLabs.Timeline.Physics absent" : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
```

**3.2 Find the active scene + SubScene(s):** run the unity-cli skill's First Command (scene path,
roots, SubScene components → their `.unity` paths). Record `parentScenePath` + `subScenePath`(s).

**3.3 Find PlayableDirector(s) inside the SubScene** (read-only additive open, restore parent
after): `FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsInactive.Include,
FindObjectsSortMode.None)` — print per director: hierarchy path, scene.path, playableAsset path or
null, other components. Selection rule when several exist (STATE it in your memory card): the
single director in the chosen SubScene; then one carrying the project's timeline-reference
authoring component; else ask the designer. Zero directors → protocol §6.

**3.4 Find the physics body (bind target) by COMPONENT, never by name:**
```csharp
var bodies = UnityEngine.Object.FindObjectsByType<Unity.Physics.Authoring.PhysicsBodyAuthoring>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
// print per body: hierarchy path, scene.path, MotionType, Mass, sibling components
// (TargetsAuthoring? StatAuthoring?) - confirm with the designer if more than one.
```
ZERO bodies in the SubScene → a missing prerequisite: a physics-stage specialist must add one; you bind bodies,
you don't create them. Prerequisites: TargetLocal/InitialLocal/LineOfSight/Flee need the BODY's
Targets slot populated (TargetsAuthoring — the stage specialist's job; a lost slot triggers the
silent fallback matrix); World needs nothing. `strengthStat` needs a stat schema asset — discover
via `AssetDatabase.FindAssets("t:StatSchemaObject")`, print each name +
`System.Convert.ToInt64(schema.Key)` (**keys drift between projects — never assume a remembered
id**), verify StatAuthoring + a StatDefaults entry for that key on the resolved entity. Missing
setup = report the gap, don't improvise.

**3.5 Capture the chosen director's existing state — this is pre-state (`PRE|`)**: print
`PRE|playableAsset=<asset PATH or null>` (via `AssetDatabase.GetAssetPath(director.playableAsset)`)
and one `PRE|binding|<i>|<track name>|<track type>|<bound object hierarchy path + component type,
or null>` line per `GetOutputTracks()` of the CURRENT asset, via `GetGenericBinding`. Capture the
asset PATH and each track's NAME/index even when the table looks empty — they make the undo
journal replayable (UNDO-1 reloads the old asset by path, re-binds by name/index). Record these in
the undo journal (§6) before any mutation.

**Name resolution rule**: `GameObject.Find` misses inactive objects and is ambiguous on duplicates.
Resolve the body/director by the recorded hierarchy path (walk the SubScene roots) or
`FindObjectsByType` filtered by `scene` — never bare `Find` unless discovery confirmed uniqueness.

## 4. CANONICAL RECIPES

One logical change per exec block; print the `PRE|` capture before mutating (protocol §2), save
inside the block, verify from a fresh load (§7). Clip fields are camelCase serialized — set via
`SerializedObject` (§2 YAML names = property paths; nested tuning = `tuning.Proportional` etc.).

```csharp
// ---- parameters (discovered in §3 / chosen with designer) ----
var parentScenePath = "<DISCOVERED>"; var subScenePath = "<DISCOVERED>";
var directorGoName  = "<DISCOVERED>"; var bodyGoPath = "<DISCOVERED>"; // PhysicsBodyAuthoring holder
var assetFolder = "<CHOSEN>"; var assetPath = assetFolder + "/<Name>.playable"; var trackName = "<CHOSEN>";
var parentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
var subScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(subScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(subScene);
try {
    // CAPTURE (print + journal): PRE|folderExisted=<bool> PRE|assetExisted=<bool>
    var folderExisted = UnityEditor.AssetDatabase.IsValidFolder(assetFolder);
    if (!folderExisted) { /* CreateFolder for each missing segment */ }
    var timeline = UnityEngine.ScriptableObject.CreateInstance<UnityEngine.Timeline.TimelineAsset>();
    UnityEditor.AssetDatabase.CreateAsset(timeline, assetPath);
    var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsLinearPIDTrack>(null, trackName);
    // Pattern WORLD HOVER: fly to / hover at an absolute point (fallback-immune, zero Targets setup)
    var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsLinearPIDClip>();
    clipA.start = 0; clipA.duration = 3; clipA.displayName = "<NAME AFTER THE POINT - field is overloaded>";
    var soA = new UnityEditor.SerializedObject((UnityEngine.Object)clipA.asset);
    soA.FindProperty("targetMode").intValue = 3;  soA.FindProperty("trackingTarget").intValue = 0; // World, None
    soA.FindProperty("targetOffset").vector3Value = new UnityEngine.Vector3(0, 3, 5);              // THE absolute goal
    soA.ApplyModifiedPropertiesWithoutUndo();
    UnityEditor.AssetDatabase.SaveAssets();
    // Wire the director (binding table lives in the SCENE file)
    var director = UnityEngine.GameObject.Find(directorGoName).GetComponent<UnityEngine.Playables.PlayableDirector>();
    // CAPTURE (print + journal) BEFORE mutating: PRE|playableAsset=... PRE|binding|... (section 3.5)
    var body = UnityEngine.GameObject.Find(bodyGoPath).GetComponent<Unity.Physics.Authoring.PhysicsBodyAuthoring>();
    director.playableAsset = timeline;
    director.SetGenericBinding(track, body);   // the COMPONENT (rule 5k); the baker coerces component->entity
    UnityEditor.EditorUtility.SetDirty(director);
    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
    return "OK|" + assetPath;
} finally {
    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(parentScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(subScene, false);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(parentScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
}
```

Other proven patterns (same route; values are choices, not constants): **follow** =
`TargetLocal(0)` + `trackingTarget=Target(1)` (Targets slot must point at the thing), offset in
the TARGET's local frame — a rotating target SWINGS the goal around it (offset (0,0,-2) = trail
behind its tail); retarget mid-timeline via EntityLinkTargetPatch (ordered before this track).
**Snapshot** ("move to where it WAS") = `InitialLocal(1)` — ONLY as the first PID clip of an
activation (capture gate, §2); offset applies in the target's frame AT CAPTURE TIME, then the goal
is a frozen world point. **Arm's length** = `LineOfSight(2)`, offset.z negative stops short /
positive overshoots; frame = `LookRotationSafe(target−self)`, yaw/pitch only. **Flee** =
`FleeFromTarget(4)` (offset ignored) — constant MaxOutput-saturated push; clamp or keep it short.
**Stat-scaled thrust** = `strengthStat=<discovered schema>` + `readStatFrom` at an entity that
ACTUALLY carries the key (×100: author 200 for double) — walk the triple trap first.

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`); parent scene
  `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`. Stage (built by
  unity-stage-foundations): `Stage_Director` (PlayableDirector + TimelineReferenceAuthoring, the
  only director); binding target **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** — pos
  (0,1,5), Dynamic, Mass=1, ForceUnique=True, `Targets.Target=Stage_Target`, **NO StatAuthoring**
  (the stat-trap layer-1 exhibit: `readStatFrom=Self` + any schema → silently ×1).
- Asset built in training: `Assets/Training/19-physics-linear-pid-track/LinearPIDMastery.playable`
  — one track `LinearPIDTrack`, clips `A_HoverAtPoint` 0–3 World offset=(0,3,5) tracking=None /
  `B_FollowCubeAbove` 4–7 TargetLocal tracking=Target offset=(0,2,0) / `C_SnapshotGoal` 8–10
  InitialLocal tracking=Target offset=(0,2,0) — kept deliberately as the TRAP exhibit (after A/B
  it seeks the stale snapshot = world origin, §2) / `D_Flee` 11–12 FleeFromTarget tracking=Target;
  all strength=1, default tuning, strengthStat=null, blendIn=-1.
- Director wiring after the lesson: table **17** entries, #17 = `LinearPIDTrack
  (PhysicsLinearPIDTrack) → Stage_PhysicsBall (PhysicsBodyAuthoring)`, prior 16 intact; director
  restored to `Assets/Training/01-transform-position-track/PositionMastery.playable`. vex-ee stat
  schema example: SlowMo, `key=94`, stage default Added=25 → reads ×0.25. Known pre-existing
  console baseline: UnityCliConnector HTTP start, PerformanceTesting setup/cleanup,
  TestResults.xml, lessons 08–10 `[Worker2]` EntityLinks bake errors.

## 6. UNDO APPENDIX

Runtime effects (velocity, PID state) exist only in play mode — nothing to undo there. Artifact
inventory for one run of §4 (vex-ee instance shown in §5):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip sub-assets — `DeleteAsset`
   removes all sub-assets with the file). 2. Possibly-created folder(s) `<assetFolder>` (only if
   `PRE|folderExisted=false`; vex-ee: `EXPECTED:` the report never printed folder pre-existence).
3. Mutated `director.playableAsset` (vex-ee: `EXPECTED:` pre value `PositionMastery.playable` —
   inferred from the report's restore step; the pre-wiring value was not printed. Capture it
   yourself per §3.5). 4. Added generic binding entry for the new track (SubScene file; vex-ee:
   "prior 16 intact" was verified post-wiring, but the 16 entries' contents were not printed
   pre-wiring — `EXPECTED:` only). 5. No other scene values changed — the recipe never edits the
   body or stage objects.

ORDER: restore the director FIRST, THEN delete the asset, THEN other captured scene values — an
asset deleted while the director points at it leaves a dangling `{fileID: 0}` in the scene file.

```csharp
// UNDO-1: restore director's captured playableAsset + binding table. Runs inside the same
// SubScene bracket as the §4 recipe (open <CAPTURED subScenePath> additive, SetActiveScene,
// try { body below } finally { restore <CAPTURED parentScenePath> Single }).
var director = UnityEngine.GameObject.Find("<CAPTURED directorGoName>").GetComponent<UnityEngine.Playables.PlayableDirector>();
var myAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Timeline.TimelineAsset>("<CAPTURED assetPath>");
foreach (var tr in myAsset.GetOutputTracks()) director.ClearGenericBinding(tr); // entries I added
// restore each CAPTURED binding (PRE|binding| lines): reload the PREVIOUS asset by captured path,
// match tracks by name/index, re-find bound objects by captured hierarchy path, SetGenericBinding.
director.playableAsset = null; // or LoadAssetAtPath<PlayableAsset>("<CAPTURED pre path>") - CAPTURED value, never "default"
UnityEditor.EditorUtility.SetDirty(director);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(subScene);
return "UNDONE|director restored";
```

```csharp
// UNDO-2: delete the created .playable (+ folder, only if PRE|folderExisted=false and now empty)
var ok = UnityEditor.AssetDatabase.DeleteAsset("<CAPTURED assetPath>");
if (!/*CAPTURED folderExisted*/false && UnityEditor.AssetDatabase.FindAssets("", new[]{ "<CAPTURED assetFolder>" }).Length == 0)
    UnityEditor.AssetDatabase.DeleteAsset("<CAPTURED assetFolder>");
return "UNDONE|deleted=" + ok;
```

UNDO-3: restore any other captured scene values — normally none for this family beyond UNDO-1.
UNDO-4 (fresh-load verification, protocol §7): reload the SubScene additively, print
`director.playableAsset` (must equal the CAPTURED pre value) and the binding table (must equal the
captured `PRE|binding|` lines); confirm `LoadAssetAtPath<Object>(assetPath) == null`; restore the
parent scene; `unity-cli console --filter error` clean against the project baseline.

## 7. VERIFICATION PROTOCOL

1. **Fresh-load asset dump** (separate exec block; in-memory state after a save is not evidence):
   load the `.playable` at `<assetPath>`, dump every track/clip (name, start/duration, blendIn/Out,
   caps, targetMode, trackingTarget, targetOffset, strength, tuning, strengthStat, readStatFrom).
   Expect `caps=Looping|Blending`; default tuning P=(10,10,10) D=(1,1,1) I=(2,2,2) Max=100.
2. **Raw YAML check**: enums as ints (`targetMode`/`trackingTarget`/`readStatFrom`), `targetOffset`
   Vector3 block, `strengthStat: {fileID: 0}` when null, `m_BlendInDuration: -1` when no overlap.
3. **Prerequisite checks**: the bound body's MotionType/Mass; its Targets slot if any clip uses a
   target-relative mode (the §2 fallback is silent); StatAuthoring + key if `strengthStat` is set.
4. **Binding from a RELOADED SubScene**: expect `BIND|<i>|<trackName> (PhysicsLinearPIDTrack) ->
   <bodyGoName> (PhysicsBodyAuthoring)` — `GetGenericBinding` returns the COMPONENT verbatim
   (rule 5k); all prior entries intact.
5. **Parent-scene restore** (sceneCount=1, `<parentScenePath>|loaded=True|active=True|dirty=False`)
   and **console**: `unity-cli console --filter error` shows nothing new beyond the project's known
   baseline (vex-ee baseline in §5). Silence is expected, not evidence (family pattern 7).
