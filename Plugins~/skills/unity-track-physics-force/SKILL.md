---
name: unity-track-physics-force
description: Master of PhysicsForceTrack + PhysicsForceClip (package BovineLabs.Timeline.Physics) — impulse/continuous forces via PendingForce with 7 direction modes, latching, deterministic per-entity randomness, pre-fire velocity reset, the one-impulse-per-activation trap. Portable to any project containing the package; worked example from vex-ee.
---

# PhysicsForceTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsForceTrack`** ("BovineLabs/Physics/Force") and
**`PhysicsForceClip`** from the package `BovineLabs.Timeline.Physics`, ns
`BovineLabs.Timeline.Physics.Authoring` — bound to a **`PhysicsBodyAuthoring` COMPONENT**. Scope:
authoring track/clips in a `.playable`, wiring a SubScene PlayableDirector, the runtime force
semantics. Physics bodies, Targets slots, and stat setups are OTHER specialists' domains. **Family
patterns 1–7 live in `unity-track-physics-filter-override`**; **PendingForce/accumulator mechanics +
the stat chain (`StatStrengthUtility.Resolve`, the triple trap) are banked in
`unity-track-physics-angular-pid`** — cite both, don't re-derive. This skill owns direction modes,
latching, randomness, the velocity reset, and the one-impulse-per-activation trap.

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor
per `unity-cli`.

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

## 3. DISCOVERY

Per `unity-timeline-track-authoring` §1 (D1–D5): D1 confirms the package
(`PhysicsForceTrack`/asm `BovineLabs.Timeline.Physics.Authoring` → `MISSING_PREREQUISITE` else);
D4's bind target is `Unity.Physics.Authoring.PhysicsBodyAuthoring` (print MotionType, Mass, sibling
TargetsAuthoring?/StatAuthoring? per body) — ZERO bodies = a physics-stage specialist's gap, you
bind one, never create it. Track-specific prerequisites to resolve the same read-only way and
report (never improvise) if missing:
- **Toward/AwayFromTarget and any non-None/non-Self `space`** need the BODY's Targets slot
  populated (TargetsAuthoring — the stage specialist's job).
- **`strengthStat`** needs a stat schema asset — `AssetDatabase.FindAssets("t:StatSchemaObject")`,
  print each name + `System.Convert.ToInt64(schema.Key)` (**keys drift between projects — never
  assume a remembered id**), and verify the resolved entity carries StatAuthoring + a StatDefaults
  entry for that key (else the silent ×1/dead-force stat layers in §2 fire).

## 4. CANONICAL RECIPES

Authored per `unity-timeline-track-authoring` §2 (the SubScene bracket). `<TRACK_TYPE>` =
`BovineLabs.Timeline.Physics.Authoring.PhysicsForceTrack`, `<CLIP_TYPE>` = `…PhysicsForceClip`,
`<BIND_TARGET>` = `Unity.Physics.Authoring.PhysicsBodyAuthoring`. Set the 18 fields via
`SerializedObject` using the §2 camelCase property paths. The track-specific MIDDLE — five proven
patterns (designer intent → wiring; values are choices, not constants):

- **IMPULSE KICK / deterministic dash** (one per timeline activation — the §2 trap): `mode=Impulse(1)`,
  `directionMode=FixedVector(0)`, `linearForce=(0,8,0)` (N·s = mass×Δv), `space=None(0)` (raw world),
  `resetVelocityOnFire=Linear(1)` (same dash distance every time). Clip e.g. start 0, duration 0.5.
- **SEEK / HOME** = `mode=Continuous(0)` + `directionMode=TowardTarget(1)`, `directionTarget=Target(1)`
  (the Targets slot must name the target — §3), `magnitude` in N — re-aims every fixed step;
  `AwayFromTarget(2)` = flee.
- **ROCKET THRUST** = Continuous + FixedVector + `space=Self(4)` (re-rotates with the body; runs
  through gaps).
- **SCATTER** = Impulse + `directionMode=RandomCone(4)` + `space=None`, `coneElevationCenter=45`,
  `coneElevationHalfRange=15`, `coneAzimuthHalfRange=180`, `seed=7`, `latchDirection=True` = any
  heading 30–60° up, seeded per (seed, entity): a debris crowd sharing one clip scatters
  differently per body, reproducibly per session.
- **BRAKE against motion** = Continuous + `AgainstVelocity(6)` + `latchDirection=false`
  (constant-magnitude retro-force vs Drag's proportional decay; latch=true locks the brake to the
  first tick's motion). **SPIN** = set `angularForce` on the same clip (one PendingForce element
  through inverse mass AND inertia). **Stat-scaled kicks** = `strengthStat` + `readStatFrom`,
  ×100 (100 = ×1.0).

## 5. WORKED EXAMPLE (delta vs the shared stage — `unity-timeline-track-authoring` §5)

Rediscover, never assume. Bind target = **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** —
pos (0,1,5), Dynamic, Mass=1, ForceUnique=True, `Targets.Target=Stage_Target`, **NO StatAuthoring**
(keeps the silent-×1 stat exhibit live). Asset
`Assets/Training/21-physics-force-track/ForceMastery.playable` — one track `ForceTrack`, clips
`A_LaunchUp` 0–0.5 Impulse FixedVector (0,8,0) space=None reset=Linear / `B_ThrustForward` 1–3
Continuous FixedVector (0,0,5) space=Self / `C_SeekCube` 4–6 Continuous TowardTarget mag=6
dirTarget=Target / `D_ScatterKick` 7–7.5 Impulse RandomCone space=None mag=10 azH=180 elC=45 elH=15
seed=7 latch=True. D doubles as the trap exhibit: it never fires in the same activation as A (§2);
B's thrust runs through the 3–4 s gap, C's seek through 6–7 s. Director wiring: table **19** entries,
`BIND|18|ForceTrack (PhysicsForceTrack) -> Stage_PhysicsBall (PhysicsBodyAuthoring)`; director
restored to its PRE value `Assets/Training/01-transform-position-track/PositionMastery.playable` (18
prior bindings intact). vex-ee stat schema example: SlowMo, `key=94`, stage default Added=25 → ×0.25.

## 6. UNDO

Per `unity-timeline-track-authoring` §3. Runtime effects (velocity, Fired state) exist only in play
mode — nothing to undo there; the journal reverses only authoring artifacts (created
`.playable` + sub-assets, possibly-created folder, mutated `director.playableAsset`, the added
generic binding). Restore the director FIRST, then delete the asset, then any other captured values
(none for this family beyond UNDO-1 — the recipe never edits the body or stage objects). Use the
§3 UNDO-1/2/3/4 templates filled from your `PRE|` captures.

## 7. VERIFICATION

Per `unity-timeline-track-authoring` §4 (fresh-load asset dump → raw YAML → live prerequisite
re-check → reloaded-SubScene binding → parent-scene restore → console). Track-specific expectations:
- §4.1 asset dump: expect `caps=Looping|Blending`; dump ALL 18 §2 fields.
- §4.2 YAML: cone fields in DEGREES (radians are bake-only); enums as ints (`mode`, `directionMode`,
  `space`, `resetVelocityOnFire`); `linearForce` blocks; `strengthStat: {fileID: 0}` when null vs an
  asset→asset ref when assigned.
- §4.3 prerequisites: bound body MotionType/Mass; its Targets slot if any clip uses Toward/Away or a
  Targets-resolved `space`; StatAuthoring + key presence if `strengthStat` is set.
- §4.4 binding: `BIND|<i>|<trackName> (PhysicsForceTrack) -> <bodyGoName> (PhysicsBodyAuthoring)`.
- Silence is expected, not evidence (family pattern 7).
