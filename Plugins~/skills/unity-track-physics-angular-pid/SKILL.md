---
name: unity-track-physics-angular-pid
description: Master of PhysicsAngularPIDTrack + PhysicsAngularPIDClip (package BovineLabs.Timeline.Physics) — a real physical rotation motor via PendingForce, blended PID configs, target-mode semantics, the stat-strength triple trap; carries the SHARED PID CORE for the linear-pid skill. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "make this body physically turn toward / match a rotation with spring-damper feel".
---

# PhysicsAngularPIDTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsAngularPIDTrack`** and **`PhysicsAngularPIDClip`** from the
package `BovineLabs.Timeline.Physics`, ns `BovineLabs.Timeline.Physics.Authoring.PIDs` — bound to
a **`PhysicsBodyAuthoring` COMPONENT** (not a GameObject). Scope: authoring track/clips in a
`.playable`, wiring a SubScene PlayableDirector, the runtime rotation-motor semantics. Physics
bodies, Targets slots, and stat setups are OTHER specialists' domains (protocol §6: report a missing prerequisite, never improvise). **Family patterns live in `unity-track-physics-filter-override`**
(two-system split, producer/modifier groups, central `PhysicsTimelineBakingSystem`,
timeline-activation scope, silence profile) — cite, don't re-derive. **This skill carries the
SHARED PID CORE** consumed by `unity-track-physics-linear-pid` (which differs ONLY in: error =
position delta, its own `PidLinearTargetMode` incl. `InitialLocal`/`LineOfSight`, `TargetOffset`
float3 instead of `TargetRotation`, and `PendingForce.Linear` instead of `.Angular`).
Behave per unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say where a fact was
PROVEN, not where it applies. (All verified vex-ee 2026-06 via reflection dumps, package-source +
raw YAML reads, fresh-load read-backs through `unity-cli exec`; no play mode — source-derived.)

While a clip is active, a PID controller computes a shortest-path axis-angle error toward a goal
derived per `PidAngularTargetMode` and appends `torque × dt` into the body's `PendingForce` buffer
— a real physical motor through mass/inertia, NEVER a direct `PhysicsVelocity` or transform write.
NO capture/restore: at timeline end the controller is disabled, the body keeps its momentum. Track
distinctions: (1) ONE shared **`PhysicsPidApplySystem`** (`PhysicsProducerGroup`,
`[UpdateAfter(PhysicsKinematicsApplySystem)]`, BEFORE the physics step) hosts BOTH
`AppendLinearJob` and `AppendAngularJob`; (2) **REAL blending** — `ClipCaps.Blending | Looping`,
`ClipWeight` baked, `PhysicsAngularPIDMixer` is genuine math (contrast Filter/Kinematic's dead
`DiscreteMixer`); (3) **no State.Original**. Runtime effects exist only in play mode.

### THE SHARED PID CORE (banked for the linear-pid skill) — literally shared source: `PidCore.cs` + `StatStrengthConfig.cs` in the Data asm; `PhysicsMath.ComputePidForce` + `StatStrengthUtility.Resolve` in the runtime asm

**PidTuning** (verbatim, incl. the tuning-order comment): `public struct PidTuning { public
float3 Proportional; public float3 Derivative; /* D before I — tune in this order */ public
float3 Integral; public float MaxOutput; }`

**PidStateData + lifecycle**: `float3 IntegralAccumulator; float3 PreviousError; float3
CapturedTargetPosition; bool IsInitialized;`. Reset to `default` by `ResetStateTrackJob` on
clip-activation edges ONLY while the Active component is disabled — back-to-back clips within one
timeline activation share accumulated state; a FRESH activation starts zeroed. First tick
(`IsInitialized==false`): `prevError = error` (zero derivative — no kick), `integral = 0`. Exit:
`DisableStaleTrackJob` disables the Active bit at TIMELINE deactivation; State sits stale until
the next activation's reset; `PhysicsVelocity` is never restored. `CapturedTargetPosition` is
angular-unused plumbing (Linear's `InitialLocal` locks the resolved target on first tick).

**ComputePidForce — the one true PID kernel** (condensed from the verbatim quote): `deltaTime <=
0` → zero output, state unchanged. `nextIntegral = ∫e + e·dt`; then **anti-windup**:
`math.all(Ki <= 0)` → accumulator HARD-ZEROED (pure PD, the "Rigid" preset); else clamped per-AXIS
to `±MaxOutput / max(Ki, 0.001)` — the I-term alone can never exceed MaxOutput per axis (the
`math.all` means mixed gains take the clamp path). `rawOutput = P·e + I·∫e + D·(e−prev)/dt`; then
the **magnitude clamp** on the vector norm (direction preserved, capped at MaxOutput). Two clamps;
do not conflate.

**Error math (angular)** — shortest-path axis-angle in radians, quoted: `delta = math.mul(target,
math.conjugate(current)); qPositive = math.select(q, -q, q.w < 0f); /* hemisphere fix */
if (math.lengthsq(qPositive.xyz) < 1e-6f) return zero; /* zero-delta guard */ angle = 2.0f *
math.acos(math.clamp(qPositive.w, -1f, 1f)); error = axis * angle; /* radians, |e| <= pi */`.
Max |error| is π — default P=10 tops the P-term out ~31, well under MaxOutput=100; the cap matters
mostly for D spikes and high-gain presets.

**Output → PendingForce, drained SAME tick through inertia** (quoted): `torque *= config.Strength
* multiplier; if (math.lengthsq(torque) > 1e-5f) pendingForces[i].Add(new PendingForce { Angular =
torque * DeltaTime });` — the micro-force skip. `PhysicsProducerForceAccumulatorSystem` (end of
PhysicsProducerGroup, explicitly `[UpdateAfter(PhysicsPidApplySystem)]` — SAME tick, no add-path
latency) sums the buffer: world torque impulse → body-local for `InverseInertia` → back to world
into `PhysicsVelocity.Angular` (linear via `InverseMass`); additive with gravity, drag, collisions.

**StatStrengthUtility.Resolve** — `StatStrengthConfig{StatKey Stat; Target ReadFrom; ushort
LinkKey}`, `IsEnabled() => Stat.Value != 0`. No schema authored → 1; entity unresolvable → 1; no
`Stat` buffer → 1; **buffer present but key absent → `GetValueFloat` default 0 — silently kills
the PID**. Value ×100-decoded (a stat authored 25 reads ×0.25). `Targets.Get` maps `Self => self`
even on a default struct — `readStatFrom=Self` always hits the body.

| Type | Facts |
|---|---|
| `PhysicsAngularPIDTrack` | ns `...Physics.Authoring.PIDs`, asm `...Physics.Authoring`, base `DOTSTrack`, EMPTY body. `[TrackClipType(typeof(PhysicsAngularPIDClip))]`, `[TrackColor(0.9,0.4,0.4)]`, **`[TrackBindingType(typeof(Unity.Physics.Authoring.PhysicsBodyAuthoring))]`**, `[DisplayName("BovineLabs/Physics/Angular PID")]`. |
| `PhysicsAngularPIDClip` | base `DOTSClip`, `ITimelineClipAsset`, **`clipCaps => ClipCaps.Blending \| ClipCaps.Looping`**, `duration => 1` (seed only). `PhysicsAngularPIDData` = `PidTuning Tuning; Target TrackingTarget; PidAngularTargetMode TargetMode; quaternion TargetRotation; float Strength; StatStrengthConfig StrengthStat;` — `PhysicsAngularPIDAnimated` = `IAnimatedComponent<PhysicsAngularPIDData>` (`AuthoredData` + `Value`, CLIP entity). |
| `ActiveAngularPid` | IComponentData + **IEnableableComponent**: `PhysicsAngularPIDData Config` — BINDING entity, added DISABLED at bake. **Lowercase "Pid"** (vs "PID" everywhere else) — fully-qualify in exec snippets. `PhysicsAngularPIDState` = `PidStateData State` on the binding. `PendingForce` = `[InternalBufferCapacity(0)]` IBufferElementData `{float3 Linear; float3 Angular;}`. |
| Systems | `PhysicsAngularPIDTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(PhysicsLinearPIDTrackSystem)]`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`); shared `PhysicsPidApplySystem` (above); `PhysicsProducerForceAccumulatorSystem` drains same tick. |

### Clip fields — camelCase serialized names, defaults from a fresh instance (reflection)
| Field | Type | Default | Meaning |
|---|---|---|---|
| `uniformAxes` | bool | `True` | EDITOR-ONLY sugar — absent from `PhysicsAngularPIDData`, never baked; `PidEditorUtility.DrawGain` writes `SetFloat3(prop, v, v, v)` when ticked; script-set uneven gains bake verbatim until the inspector flattens them. |
| `tuning` | PidTuning | P=(10,10,10) D=(1,1,1) I=(2,2,2) MaxOutput=100 | Matches NO preset — underdamped vs "Balanced", integral-heavier. |
| `trackingTarget` | Target | `Target(1)` | Which `Targets` slot ON THE BODY names the target entity. |
| `targetMode` | PidAngularTargetMode | `LookAtTarget(1)` | How the goal rotation is derived. |
| `targetRotationEuler` | Vector3 | `(0,0,0)` | Euler **DEGREES**; baked `quaternion.Euler(math.radians(...))`. World mode: absolute; all other modes: a post-multiplied OFFSET. |
| `strength` | float | `1` `[Min(0)]` | "Output force multiplier. 0 = no effect, 1 = full, 2 = double." |
| `strengthStat` / `readStatFrom` / `readStatLink` | StatSchemaObject / Target / EntityLinkSchema | `null` / `Self(4)` / `null` | Optional ×100-fixed-point stat MULTIPLIER; whose stat buffer (via the BODY's Targets); optional link override for the stat hunt. |

Enums (live `Enum.GetValues` — re-dump in YOUR project): `PidAngularTargetMode`: **`MatchTarget=0,
LookAtTarget=1, World=2, FleeFromTarget=3, MatchTargetOpposite=4`**; `Target` (Reaction, byte):
`None=0, Target=1, Owner=2, Source=3, Self=4, Custom=6`. YAML: enums as ints, euler in authored
DEGREES, tuning as nested float3 blocks, strengthStat as a normal asset→asset ref. Bake:
unconditional, totally SILENT (family pattern 7); the central `PhysicsTimelineBakingSystem`
(`Entity.Null` continue when unbound) adds `ActiveAngularPid`(disabled) + `PhysicsAngularPIDState`
+ `PendingForce`/`PendingVelocity` buffers + disabled `PendingVelocityReset`.

### Target resolution (quoted) — the mode table + self-fallback
```csharp
if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
    targetEntity = targets.Get(config.TrackingTarget, entity);
if (!hasTargetTransform) { targetPos = selfPos; targetRot = selfRot; }   // FALLBACK TO SELF
targetRotation = config.TargetMode switch {
    MatchTarget => math.mul(targetRot, config.TargetRotation),  World => config.TargetRotation,
    LookAtTarget => ResolveLookAtTarget(selfPos, targetPos, selfRot, config.TargetRotation),
    FleeFromTarget => ResolveLookAtTarget(selfPos, selfPos + (selfPos - targetPos), selfRot, config.TargetRotation),
    MatchTargetOpposite => math.mul(math.mul(targetRot, quaternion.AxisAngle(math.up(), math.PI)), config.TargetRotation),
    _ => selfRot };                                                       // unknown mode => zero error
```

LookAt uses **`LookRotationSafe`** (no NaN poisoning — contrast the transform-rotation track's
unguarded `LookRotation`). `None` short-circuits BEFORE the Targets lookup (World's natural
pairing). `TargetRotation` is absolute ONLY in World mode; elsewhere a post-multiplied OFFSET.

### Tuning doctrine — order **P, then D, then I**. Tooltips verbatim: P "How hard the controller pushes. Raise until it reaches the goal." D "Kills oscillation. Raise after P until stable. Too high = sluggish." I "Only add if the entity stalls short of the goal. Too high = slow oscillation." MaxOutput "Hard cap on output each frame. Prevents explosive behaviour." `PidEditorUtility` presets:

| Preset | Description | P | D | I | Max |
|---|---|---|---|---|---|
| Snappy | Fast with slight overshoot | 20 | 4 | 0.5 | 200 |
| Balanced | Smooth, no overshoot | 10 | 3 | 1 | 100 |
| Floaty | Gentle, large overshoot | 4 | 0.5 | 0.2 | 40 |
| Heavy | High force, well-damped | 30 | 10 | 1 | 400 |
| Precise | Slow but kills drift | 8 | 4 | 5 | 80 |
| Rigid | Near-kinematic feel | 60 | 20 | 0 | 1000 |

### Edge cases & traps (each source-proven, vex-ee 2026-06)

- **DON'T read fallback-to-self as one behavior — it is MODE-DEPENDENT** — unresolvable target
  silently sets goal-from-self, no log: MatchTarget+identity-offset = true quiet no-op (zero error
  → guard → micro-skip); MatchTarget+non-identity offset = the body **chases its own offset
  forever**; LookAtTarget = `LookRotationSafe` re-derives up, so a rolled body gets an
  **up-righting torque**; World = fallback INVISIBLE (targetRot unused).
- **DO triage "PID does nothing" as a lost target** — check the BODY entity's `Targets` component
  (read on the BINDING, not the director, not via readRootFrom like the EntityLinks family).
- **DON'T trust the stat multiplier without walking the THREE-layer trap** — no Stat buffer →
  multiplier silently **1** (a stat-driven clip behaves as a constant, no warning); buffer exists
  → ×100-decoded (authored 25 = **×0.25, not ×25**; author 200 for double); buffer present but key
  ABSENT → `GetValueFloat` **0** → `torque *= strength × 0` — PID silently dead (buffer-vs-key).
- **DO expect REAL blending — one controller, never two** — overlap blends at CONFIG level through
  `PhysicsAngularPIDMixer` (Lerp: goal **slerped**, tuning/strength **lerped**,
  TrackingTarget/TargetMode/StrengthStat **snap at s=0.5**; Add: gains/strength summed, quaternion
  log/exp rotation sum, enums/stat from the dominant clip — higher Strength wins, tie → lower
  TargetMode byte); the apply system consumes ONE blended `ActiveAngularPid.Config`, one
  `PidStateData`. A LookAt→MatchTarget crossfade flips derivation mid-blend, goal slerp-continuous.
- **DON'T treat gaps or exit as neutral** — family pattern 5: between clips the last Config keeps
  seeking; at timeline end `DisableStaleTrackJob` merely disables the bit — **momentum survives**
  (VelocityClamp or a Kinematic freeze if the spin must die at the cut).
- **DO know tiny outputs vanish** (`lengthsq(torque) > 1e-5f` micro-force skip + 1e-6 zero-delta
  guard: a near-settled body appends nothing) and **the silence profile holds** (family rule 7:
  no guard or LogError in Bake; null strengthStat harmless via `IsEnabled()==false`; unbound track
  → central-baker continue — clean console proves nothing).

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never play mode;
unity-cli Safe Loop on every mutation. Names below are parameters — discover them in THIS project;
never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDTrack, BovineLabs.Timeline.Physics.Authoring");
return t == null ? "MISSING_PREREQUISITE|PhysicsAngularPIDTrack not found - package BovineLabs.Timeline.Physics absent" : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
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
you don't create them. Prerequisites: MatchTarget/LookAtTarget/Flee/MatchTargetOpposite need the
BODY's Targets slot populated (TargetsAuthoring — the stage specialist's job; a lost slot triggers
the silent fallback); World needs nothing. `strengthStat` needs a stat schema asset — discover via
`AssetDatabase.FindAssets("t:StatSchemaObject")`, print each name +
`System.Convert.ToInt64(schema.Key)` (**keys drift between projects — never assume a remembered
id**), verify StatAuthoring + a StatDefaults entry for that key. Missing setup = report the gap.

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
    var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDTrack>(null, trackName);
    // Pattern WORLD: settle at an absolute world rotation (no Targets needed at all)
    var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDClip>();
    clipA.start = 0; clipA.duration = 3; clipA.displayName = "<clipName>";
    var soA = new UnityEditor.SerializedObject((UnityEngine.Object)clipA.asset);
    soA.FindProperty("targetMode").intValue = 2;  soA.FindProperty("trackingTarget").intValue = 0; // World, None
    soA.FindProperty("targetRotationEuler").vector3Value = new UnityEngine.Vector3(0, 90, 0);      // DEGREES
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

Other proven patterns (same route; values are choices, not constants): **face the target** =
`LookAtTarget(1)` + `trackingTarget=Target(1)` (Targets slot must point at it — TargetsAuthoring,
or retarget mid-timeline with EntityLinkTargetPatch, ordered before this track); start from
Balanced (P10 D3 I1 Max100), raise P for urgency, then D to kill wobble; `strength=2` doubles
output. **Ride alignment** = `MatchTarget(0)` + trackingTarget=Target; `targetRotationEuler` as a
deliberate offset if the meshes' forward axes disagree; `MatchTargetOpposite(4)` = 180°-yawed
(face-off). **Cower** = `FleeFromTarget(3)` — look-at the mirror point `self + (self − target)`;
same up-righting caveat as LookAt. **Stat-scaled turning** = `strengthStat=<discovered schema>` +
readStatFrom at an entity ACTUALLY carrying a Stat buffer WITH that key (×100 — author 200 for
double); avoid `readStatFrom=Self` unless the body has stats. **Ending crisp**: a PID clip ending
leaves spin — VelocityClamp, or accept the handoff.

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`); parent scene
  `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`. Stage (built by
  unity-stage-foundations): `Stage_Director` (PlayableDirector + TimelineReferenceAuthoring, the
  only director); binding target **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** — the
  program's first non-GameObject physics binding; Dynamic, Mass=1, `Targets.Target=Stage_Target`
  (TargetsAuthoring), **NO StatAuthoring** (the stat-trap layer-1 exhibit).
- Asset built: `Assets/Training/18-physics-angular-pid-track/AngularPIDMastery.playable` — one
  track `AngularPIDTrack` (`resetOnDeactivate: 1` inherited), clips `A_World90Yaw` 0–3 World
  euler=(0,90,0) tracking=None / `B_LookAtTarget` 4–7 LookAtTarget tracking=Target strength=2 /
  `C_StatDriven` 8–10 MatchTarget tracking=Target strengthStat=SlowMo readStatFrom=Self — the
  stat-trap exhibit (stat-less ball → silently ×1). Director wiring after the lesson: table **16**
  entries, #16 = `AngularPIDTrack (PhysicsAngularPIDTrack) → Stage_PhysicsBall
  (PhysicsBodyAuthoring)`, prior 15 intact; director restored to
  `Assets/Training/01-transform-position-track/PositionMastery.playable`. vex-ee stat schema:
  SlowMo, `key=94`, guid 95a61e8c263e49dbbc6a31729d0815ce, stage default Added=25 → reads ×0.25.
  Console baseline: UnityCliConnector HTTP start, PerformanceTesting setup/cleanup,
  TestResults.xml, lessons 08–10 `[Worker2]` EntityLinks bake errors.

## 6. UNDO APPENDIX

Runtime effects (spin, PID state) exist only in play mode — nothing to undo there. Artifact
inventory for one run of §4 (vex-ee instance shown in §5):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip sub-assets — `DeleteAsset`
   removes all sub-assets with the file). 2. Possibly-created folder(s) `<assetFolder>` (only if
   `PRE|folderExisted=false`; vex-ee: `EXPECTED:` the report never printed folder pre-existence).
3. Mutated `director.playableAsset` (vex-ee: `EXPECTED:` pre value `PositionMastery.playable` —
   inferred from the report's restore step; the pre-wiring value was not printed. Capture it
   yourself per §3.5). 4. Added generic binding entry for the new track (SubScene file; vex-ee:
   "prior 15 intact" was verified post-wiring, but the 15 entries' contents were not printed
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
   load the `.playable` at `<assetPath>`, dump every track/clip (name, start/duration, caps,
   targetMode, trackingTarget, targetRotationEuler, strength, tuning, strengthStat, readStatFrom).
   Expect `caps=Blending|Looping`; default tuning P=(10,10,10) D=(1,1,1) I=(2,2,2) Max=100.
2. **Raw YAML check**: enums as ints, `targetRotationEuler` in DEGREES (radians are bake-only),
   tuning as nested float3 blocks, `strengthStat: {fileID: 0}` when null vs asset→asset ref,
   `m_BlendInDuration: -1` when no overlap authored.
3. **Prerequisite checks**: bound body's MotionType/Mass; its Targets slot if any clip uses a
   target-relative mode (the §2 fallback is silent); StatAuthoring + key if `strengthStat` is set.
4. **Binding from a RELOADED SubScene**: expect `BIND|<i>|<trackName> (PhysicsAngularPIDTrack) ->
   <bodyGoName> (PhysicsBodyAuthoring)` — `GetGenericBinding` returns the COMPONENT verbatim
   (rule 5k); all prior entries intact.
5. **Parent-scene restore** (sceneCount=1, `<parentScenePath>|loaded=True|active=True|dirty=False`)
   and **console**: `unity-cli console --filter error` shows nothing new beyond the project's known
   baseline (vex-ee baseline in §5). Silence is expected, not evidence (family pattern 7).
