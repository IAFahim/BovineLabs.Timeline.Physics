---
name: unity-track-physics-force
description: Master of PhysicsForceTrack + PhysicsForceClip (package BovineLabs.Timeline.Physics) — impulse/continuous forces via PendingForce with 7 direction modes, latching, deterministic per-entity randomness, pre-fire velocity reset, the one-impulse-per-activation trap. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "kick / launch / thrust / knock back / scatter this body".
---

# PhysicsForceTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsForceTrack`** ("BovineLabs/Physics/Force") and
**`PhysicsForceClip`** from the package `BovineLabs.Timeline.Physics`, ns
`BovineLabs.Timeline.Physics.Authoring` — bound to a **`PhysicsBodyAuthoring` COMPONENT**. Scope:
authoring track/clips in a `.playable`, wiring a SubScene PlayableDirector, the runtime force
semantics. Physics bodies, Targets slots, and stat setups are OTHER specialists' domains (protocol
§6: report a missing prerequisite, never improvise). **Family patterns 1–7 live in
`unity-track-physics-filter-override`**; **PendingForce/accumulator mechanics + the stat chain
(`StatStrengthUtility.Resolve`, the triple trap) are banked in `unity-track-physics-angular-pid`**
— cite both, don't re-derive. This skill owns direction modes, latching, randomness, the velocity
reset, and the one-impulse-per-activation trap.
Behave per unity-agent-protocol; operate the editor per unity-cli.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say where a fact was
PROVEN, not where it applies. (All verified vex-ee 2026-06 via reflection dumps, package-source and
raw YAML reads, fresh-load read-backs through `unity-cli exec`; no play mode — source-derived.)

| Type | Facts |
|---|---|
| `PhysicsForceTrack` | ns + asm `BovineLabs.Timeline.Physics.Authoring`, base `DOTSTrack`, EMPTY body. `[TrackClipType(typeof(PhysicsForceClip))]`, `[TrackColor(0.8,0.4,0.2)]`, `[TrackBindingType(typeof(Unity.Physics.Authoring.PhysicsBodyAuthoring))]`, `[DisplayName("BovineLabs/Physics/Force")]`. |
| `PhysicsForceClip` | base `DOTSClip`, `ITimelineClipAsset`, `clipCaps => ClipCaps.Blending \| ClipCaps.Looping` (REAL mixer), `duration => 1` (seed only). Bake: unconditional, SILENT (family pattern 7); cone degrees→radians at bake (`math.radians`); link schemas → ushort keys, null → 0. `PhysicsForceData` = the 18-field config baked verbatim (cone fields in **radians**). |
| `PhysicsForceState` | IComponentData on BINDING: `bool Fired; bool ResetApplied; bool DirectionLatched; float3 LatchedDirection;` |
| `PhysicsForceRandom` | IComponentData on BINDING: `Random Value` — **separate from State by design** (doc comment verbatim): *"Lives outside PhysicsForceState so clip re-activation resets fire/latch state without rewinding the stream — each activation draws fresh values. A zero state is lazily seeded from (Seed, entity), so a given entity and seed always replays the same sequence."* The only family branch adding a Random component. |
| `ActiveForce` | IComponentData + IEnableableComponent: `PhysicsForceData Config` — BINDING entity, added DISABLED at bake, plus `PendingForce`/`PendingVelocity` buffers + `PendingVelocityReset` (disabled) via `EnsureAccumulationBuffers`. |
| Enums | `PhysicsForceMode : byte {Continuous=0, Impulse=1}`; `PhysicsForceDirectionMode : byte {FixedVector=0, TowardTarget=1, AwayFromTarget=2, RandomSphere=3, RandomCone=4, AlongVelocity=5, AgainstVelocity=6}`; `VelocityResetFlags : byte {None=0, Linear=1, Angular=2, Both=3}`. |
| Systems | `PhysicsForceTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`) = family kernel (ResetState → Prepare → DisableStale → blend → WriteActive via BeginSim ECB). Apply: `AppendForceJob` in **`Kinematics/PhysicsKinematicsApplySystem.cs`** (`PhysicsProducerGroup`, before the physics step). `PhysicsProducerForceAccumulatorSystem` (`[UpdateAfter(PhysicsKinematicsApplySystem)]`, `[UpdateAfter(PhysicsPidApplySystem)]`) drains reset + buffers the SAME tick; twin `PhysicsModifierForceAccumulatorSystem` after the step. **Naming oddity (file-level confirmed)**: NO `PhysicsForceApplySystem` exists — `AppendForceJob` AND the Velocity track's `AppendVelocityJob` share `PhysicsKinematicsApplySystem.cs` ("Kinematics" = umbrella for direct force/velocity application; the family's second shared-apply pair). |

### Clip fields — ALL 18, camelCase serialized names, defaults from a fresh instance (reflection `FIELDCOUNT|18`; the curriculum's "17" was an erratum)

| Field | Type | Default | Meaning / tooltip (verbatim where quoted) |
|---|---|---|---|
| `mode` | PhysicsForceMode | `Impulse(1)` | *"Impulse mode applies force exactly once per clip activation and ignores Looping."* |
| `directionMode` | PhysicsForceDirectionMode | `FixedVector(0)` | How the linear force direction is derived. |
| `linearForce` | Vector3 | `(0,0,0)` | FixedVector mode's force (N for Continuous, N·s for Impulse). |
| `space` | Target | `Self(4)` | Frame for FixedVector/RandomCone/angularForce: None=raw world, Self=body frame, others via Targets. |
| `magnitude` | float | `10` | Scale for ALL direction-derived modes (Toward/Away/RandomSphere/RandomCone/Along/Against). |
| `directionTarget` | Target | `Target(1)` | Targets slot (on the BODY) naming the seek/flee target. |
| `directionTargetLink` | EntityLinkSchema | `null` | Optional link hop from the resolved slot entity. |
| `coneAzimuthCenter` | float | `0` | *"Azimuth 0 points along +Z of the Space frame; 180 points behind it."* Degrees. |
| `coneAzimuthHalfRange` | float | `30` `[Range(0,180)]` | Degrees. |
| `coneElevationCenter` | float | `0` | Degrees. |
| `coneElevationHalfRange` | float | `15` `[Range(0,89)]` | Degrees. |
| `seed` | uint | `0` | *"Offsets this body's random stream. 0 is valid; entity identity already decorrelates bodies."* |
| `latchDirection` | bool | `True` | *"Sample random/velocity-relative directions once per clip activation and hold them. Disable to re-evaluate every fire."* |
| `resetVelocityOnFire` | VelocityResetFlags | `None(0)` | *"Zeroes the body's velocity once per clip activation, immediately before this force lands. Use Linear for dashes that must always travel the same distance."* |
| `angularForce` | Vector3 | `(0,0,0)` | Torque, resolved through the same `space` frame; rides the same PendingForce element. |
| `strengthStat` | StatSchemaObject | `null` | Family ×100 fixed-point stat multiplier — gates EARLY (below). |
| `readStatFrom` | Target | `Self(4)` | Whose Stat buffer. |
| `readStatLink` | EntityLinkSchema | `null` | Optional link override for the stat hunt. |

YAML keeps **degrees** — radians exist only in baked data (raw-read verified).

### Runtime semantics (source-derived)

Per rendered frame, the family kernel: `ResetStateTrackJob` resets `PhysicsForceState` to
`{Fired=false}` (zeroing ResetApplied/DirectionLatched/LatchedDirection) on a clip-activation edge
**only while `ActiveForce` is disabled**; clips blend through the real `PhysicsForceMixer` into one
enabled `ActiveForce{Config}` (BeginSim ECB — one rendered frame of enable latency);
`DisableStaleTrackJob` disables it only at TIMELINE deactivation. Per fixed step, `AppendForceJob`
walks enabled bindings: Impulse skips if `state.Fired`; Continuous skips if `DeltaTime <= 0.0001f`;
the stat multiplier resolves EARLY (`math.abs(multiplier) < 1e-5f` skips everything); the direction
resolves per mode (fixed vectors via `ResolveSpaceVector`, target modes via `Targets`+optional
link, random/velocity modes via the latch-aware `TryResolveDynamicDirection`, which can return
false = defer); a first-fire `resetVelocityOnFire` request ORs into `PendingVelocityReset`; then
ONE `PendingForce{Linear, Angular}` element is appended scaled by `timeScale = Impulse ? 1 :
DeltaTime` and the multiplier, and Impulse sets `Fired=true`. The SAME tick the accumulator applies
the reset first, then sums PendingForce through `InverseMass`/`InverseInertia` (angular
world→local→world) — the reset always lands immediately before the force. Exit restores nothing:
momentum kept, State lazily reset on the next activation's first clip edge, Random never reset.

### THE LATCH/DETERMINISM MATRIX — only the `default:` arm of `TryResolveDynamicDirection` reaches the latch logic

| directionMode | Latches? | latchDirection=true | latchDirection=false | Stationary/failure behavior |
|---|---|---|---|---|
| FixedVector | **never** | n/a — re-resolved through `ResolveSpaceVector` every fire (Self-space thrust follows live body rotation) | same | never fails |
| Toward / AwayFromTarget (negated) | **never** | n/a — re-aimed every fire (true homing for Continuous) | same | `distSq <= 1e-5f` → **defer** (returns false; overlap with target = no force) |
| RandomSphere | yes | ONE `NextFloat3Direction()` draw per state-reset, held in `state.LatchedDirection` | re-rolls EVERY fire (Continuous = new direction every fixed step — noise thrust) | never fails |
| RandomCone | yes | ONE (az,el) pair (2 `NextFloat` draws), resolved through `ResolveSpaceVector`, held | re-rolls every fire, 2 draws per fire | never fails (elevation clamped to ±(π/2−0.01)) |
| Along / AgainstVelocity (negated) | yes | first MOVING tick's direction latched, then held even if velocity changes | re-derived from live velocity every fire | `!hasVelocity` or `speedSq <= 1e-8f` → **defer** |

Latch block verbatim: `if (config.LatchDirection && state.DirectionLatched) { direction =
state.LatchedDirection; return true; } ... if (config.LatchDirection) { state.DirectionLatched =
true; state.LatchedDirection = direction; }`. **Latched values are post-resolve WORLD vectors**:
RandomCone's `ResolveSpaceVector` runs BEFORE latching — a latched Self-space cone direction does
NOT follow later body rotation (contrast FixedVector+Self, re-rotated every fire). **Re-latch =
lazy reset**: DirectionLatched clears only on the next activation's first clip edge.

**Seed mechanics** (`NextRandom`, verbatim): `rng = hasRandom ? randoms[i].Value : default;
if (rng.state == 0) { rng = Random.CreateFromIndex(math.hash(new uint3(seed, (uint)body.Index,
(uint)body.Version))); if (rng.state == 0) rng.state = 0x6E624EB7; }` — deterministic per
`(seed, entity.Index, entity.Version)`; the sentinel guards the astronomically-unlikely zero hash
(zero state doubles as "not yet seeded"); the first random fire after subscene load lazily seeds
the stream, which then only ever advances. **Stream persistence — the honest determinism story**:
`PhysicsForceRandom` is deliberately NOT in State and never reset — within a session, activations
consume the stream (run 1's scatter kick is draw pair #1, run 2's is pair #2 — *different
direction*); "same seed → same kick" holds only for the n-th activation across SESSIONS (fresh
world → re-seed → identical sequence). A `latchDirection=false` Continuous random clip advances
the stream every fixed step, leaving later random clips at a scrub-dependent cursor. Determinism
is **per-session-at-activation-index**: latching levers WITHIN an activation, the seed ACROSS
sessions, nothing across activations in-session.

### THE ONE-IMPULSE-PER-ACTIVATION TRAP

`Fired` is **binding-level**, cleared by `ResetStateTrackJob` only while `ActiveForce` is disabled —
which only `DisableStaleTrackJob` (TIMELINE deactivation) ever does. With multiple force clips on a
binding, only the FIRST clip's edge sees a reset; later clips inherit the run's State:

- **Two impulse clips on one track = the first one only** (proven live, §5): the first Impulse sets
  `Fired=true`; later Impulse edges skip the reset, so `if (Impulse && state.Fired) continue;`
  blocks them. N impulses on one binding need a timeline restart or PhysicsTriggerForce/Velocity.
- Loop wraps are NOT activation edges: a looping Impulse stays `Fired` (the tooltip's "ignores
  Looping" = the Fired guard). Scrubbing back re-fires nothing; a full stop+replay is a fresh
  activation and re-fires/re-latches.
- **Deferred impulses can fire OUTSIDE their clip window**: a defer never sets `Fired`; with
  `ActiveForce` enabled through gaps, a stationary body's AlongVelocity impulse can land after its
  clip ends, until the next clip overwrites Config or timeline end. `ResetApplied` (the reset's
  once-guard) shares Fired's binding lifecycle.

### Edge cases & traps (each source-quoted, vex-ee 2026-06)

- **DO know the units: Impulse = N·s, Continuous = N** — `timeScale = Impulse ? 1f : DeltaTime`,
  then `velocity.Linear += totalLinear * mass.InverseMass`. With Mass=1: Impulse (0,8,0) = 8 N·s →
  **Δv = 8 m/s instantly, once** (apex ≈ v²/2g ≈ 3.26 m); Continuous (0,0,5) = 5 N → 0.1 N·s per
  50 Hz step → **a = 5 m/s², ≈ +10 m/s over a 2 s clip**.
- **DON'T expect a stationary AlongVelocity Impulse to fire on time — it WAITS** — `speedSq <=
  1e-8f → return false` → `continue` → Fired never set; it retries every fixed step and fires on
  the first moving tick, possibly after the clip window. Toward/Away defer at `distSq <= 1e-5f`; a
  body without `PhysicsVelocity` defers forever.
- **DON'T trust the stat without the early-gate walk** — `math.abs(multiplier) < 1e-5f → continue`
  resolves BEFORE direction, reset, and Fired: buffer-present-key-absent = silent dead force;
  **negative stats pass `math.abs` and INVERT the force** (contrast Drag's ≥0 clamp); a stat-dead
  Impulse is DEFERRED, not consumed — it fires if the stat goes nonzero (the stream doesn't advance
  while gated).
- **DO pick the space frame deliberately** — `ResolveSpaceVector`'s three paths: `None` = raw world
  vector (NOT "no target" — inverted vs the Targets convention `Get(None)=Null`, the Target.None
  two-meanings trap); `Self` skips the Targets lookup, rotates by the body's own orientation; any
  other Target resolves the slot's rotation (missing Targets/empty slot silently falls back to the
  body itself). `angularForce` uses the same frame.
- **DON'T treat gaps as neutral** — family pattern 5: nothing disables `ActiveForce` between
  clips; a Continuous clip's force runs through gaps, a final one until timeline end.
- **DO rely on the velocity reset's ordering** — the request ORs into `PendingVelocityReset`; the
  accumulator applies resets STRICTLY BEFORE forces in the same job (`ApplyReset` precedes
  `AccumulateForces`), once per activation (`!state.ResetApplied`). Masks are whole linear/angular
  blocks, not per-XYZ; the reset also erases what other tracks put into velocity — by design.
- **DON'T overlap Continuous and Impulse clips** — the mixer lerps every numeric (forces, magnitude,
  cone fields) but SNAPS discrete fields at `s < 0.5f ? a : b`; the moment the blended mode becomes
  Impulse with Fired false it fires the half-blended force. `Add` sums numerics, keeps A's
  discrete fields/seed/Strength.
- **DO note the silence profile (family rule 7)** — Bake unconditional; unbound track →
  central-baker silent no-op. Clean console proves nothing.

## 3. DISCOVERY RECIPES

Act only through `unity-cli exec` / `unity-cli console`; never the filesystem; never play mode;
unity-cli Safe Loop on every mutation. Names below are parameters — discover them in THIS project;
never assume the worked example (§5).

**3.1 Confirm the package exists (else report a missing prerequisite — protocol §6):**
```csharp
var t = System.Type.GetType("BovineLabs.Timeline.Physics.Authoring.PhysicsForceTrack, BovineLabs.Timeline.Physics.Authoring");
return t == null ? "MISSING_PREREQUISITE|PhysicsForceTrack not found - package BovineLabs.Timeline.Physics absent" : "OK|" + t.AssemblyQualifiedName + "|dataPath=" + UnityEngine.Application.dataPath;
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
you don't create them. Prerequisites: Toward/AwayFromTarget and any non-None/non-Self `space` need
the BODY's Targets slot populated (TargetsAuthoring — the stage specialist's job); `strengthStat`
needs a stat schema asset — discover via `AssetDatabase.FindAssets("t:StatSchemaObject")`, print
each name + `System.Convert.ToInt64(schema.Key)` (**keys drift between projects — never assume a
remembered id**), verify the resolved entity carries StatAuthoring + a StatDefaults entry for that
key (else the silent stat layers in §2 fire). Missing setup = report the gap, don't improvise.

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
`SerializedObject` (the YAML names in §2 are the property paths).

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
    var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.PhysicsForceTrack>(null, trackName);
    // Pattern IMPULSE KICK / deterministic dash (one per timeline activation - trap in section 2)
    var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.PhysicsForceClip>();
    clipA.start = 0; clipA.duration = 0.5; clipA.displayName = "<clipName>";
    var soA = new UnityEditor.SerializedObject((UnityEngine.Object)clipA.asset);
    soA.FindProperty("mode").intValue = 1; soA.FindProperty("directionMode").intValue = 0;  // Impulse, FixedVector
    soA.FindProperty("linearForce").vector3Value = new UnityEngine.Vector3(0, 8, 0);        // N*s = mass x dv
    soA.FindProperty("space").intValue = 0;                                                 // Target.None = raw world
    soA.FindProperty("resetVelocityOnFire").intValue = 1;                                   // Linear: same dash every time
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

Other proven patterns (same route; values are choices, not constants): **seek/home** = Continuous +
TowardTarget, `directionTarget=Target(1)` (Targets slot must name the target, §3.4), `magnitude` =
N — re-aims every fixed step; `AwayFromTarget` = flee. **Rocket thrust** = Continuous + FixedVector
+ `space=Self(4)` (re-rotates with the body; runs through gaps). **Scatter** = Impulse + RandomCone
+ `space=None`, elC=45/elH=15, azH=180 = any heading 30–60° up — seeded per (seed, entity): a
debris crowd sharing one clip scatters differently per body, reproducibly per session. **Brake
against motion** = Continuous + AgainstVelocity + `latchDirection=false` (constant-magnitude
retro-force vs Drag's proportional decay; latch=true locks the brake to the first tick's motion).
**Spin** = `angularForce` on the same clip (one PendingForce element through inverse mass AND
inertia). **Stat-scaled kicks** = `strengthStat` + `readStatFrom`, ×100 (100 = ×1.0).

## 5. WORKED EXAMPLE (vex-ee training stage) — example environment; rediscover, never assume

- Project `/home/i/GitHub/vex-ee` (`dataPath=/home/i/GitHub/vex-ee/Assets`); parent scene
  `Assets/Scenes/Main Scene.unity`; SubScene `Assets/Scenes/Main Sub Scene.unity`. Stage (built by
  unity-stage-foundations): `Stage_Director` (PlayableDirector + TimelineReferenceAuthoring, the
  only director); binding target **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** — pos
  (0,1,5), Dynamic, Mass=1, ForceUnique=True, `Targets.Target=Stage_Target`, **NO StatAuthoring**
  (keeps the silent-×1 stat exhibit live).
- Asset built in training: `Assets/Training/21-physics-force-track/ForceMastery.playable` — one
  track `ForceTrack`, clips `A_LaunchUp` 0–0.5 Impulse FixedVector (0,8,0) space=None reset=Linear /
  `B_ThrustForward` 1–3 Continuous FixedVector (0,0,5) space=Self / `C_SeekCube` 4–6 Continuous
  TowardTarget mag=6 dirTarget=Target / `D_ScatterKick` 7–7.5 Impulse RandomCone space=None mag=10
  azH=180 elC=45 elH=15 seed=7 latch=True. D doubles as the trap exhibit: it never fires in the
  same activation as A (§2); B's thrust runs through the 3–4 s gap, C's seek through 6–7 s.
- Director wiring after the lesson: table **19** entries, `BIND|18|ForceTrack (PhysicsForceTrack)
  -> Stage_PhysicsBall (PhysicsBodyAuthoring)`; director restored to
  `Assets/Training/01-transform-position-track/PositionMastery.playable` (its PRE value, printed
  before wiring: PositionMastery + 18 bindings). vex-ee stat schema example: SlowMo, `key=94`,
  stage default Added=25 → reads ×0.25. Known pre-existing console baseline: UnityCliConnector
  HTTP start, PerformanceTesting setup/cleanup, TestResults.xml, lessons 08–10 `[Worker2]`
  EntityLinks bake errors.

## 6. UNDO APPENDIX

Runtime effects (velocity, fired state) exist only in play mode — nothing to undo there. Artifact
inventory for one run of §4 (vex-ee instance shown in §5):
1. Created asset `<assetPath>` (.playable: TimelineAsset + track + clip sub-assets — `DeleteAsset`
   removes all sub-assets with the file). 2. Possibly-created folder(s) `<assetFolder>` (only if
   `PRE|folderExisted=false`; vex-ee: `EXPECTED:` the report never printed folder pre-existence).
3. Mutated `director.playableAsset` (vex-ee: VERIFIED pre value `PositionMastery.playable`, printed
   before wiring). 4. Added generic binding entry for the new track (table lives in the SubScene
   file; vex-ee: VERIFIED 18 prior entries intact before/after). 5. No other scene values changed —
   the recipe never edits the body or stage objects.

ORDER: restore the director FIRST, THEN delete the asset, THEN other captured scene values —
deleting the asset while the director still points at it leaves a dangling `{fileID: 0}` reference
in the scene file instead of the captured pre-state.

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
   caps, and ALL 18 fields per §2's table). Expect `caps=Looping|Blending`.
2. **Raw YAML check**: cone fields in DEGREES (radians are bake-only), enums as ints (`mode`,
   `directionMode`, `space`, `resetVelocityOnFire`), `linearForce` blocks,
   `strengthStat: {fileID: 0}` when null vs an asset→asset ref when assigned.
3. **Prerequisite checks**: the bound body's MotionType/Mass; its Targets slot if any clip uses
   Toward/Away or a Targets-resolved `space`; StatAuthoring + key presence if `strengthStat` is set.
4. **Binding from a RELOADED SubScene**: expect `BIND|<i>|<trackName> (PhysicsForceTrack) ->
   <bodyGoName> (PhysicsBodyAuthoring)` — `GetGenericBinding` returns the COMPONENT verbatim
   (rule 5k); all prior entries intact.
5. **Parent-scene restore** (sceneCount=1, `<parentScenePath>|loaded=True|active=True|dirty=False`)
   and **console**: `unity-cli console --filter error` shows nothing new beyond the project's known
   baseline (vex-ee baseline in §5). Silence is expected, not evidence (family pattern 7).
