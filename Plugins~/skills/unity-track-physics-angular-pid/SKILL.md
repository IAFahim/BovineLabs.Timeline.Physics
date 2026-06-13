---
name: unity-track-physics-angular-pid
description: Master of PhysicsAngularPIDTrack + PhysicsAngularPIDClip (package BovineLabs.Timeline.Physics) — a real physical rotation motor via PendingForce, blended PID configs, target-mode semantics, the stat-strength triple trap; carries the SHARED PID CORE for the linear-pid skill. Portable to any project containing the package; worked example from vex-ee.
---

# PhysicsAngularPIDTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsAngularPIDTrack`** and **`PhysicsAngularPIDClip`** from the
package `BovineLabs.Timeline.Physics`, ns `BovineLabs.Timeline.Physics.Authoring.PIDs` — bound to
a **`PhysicsBodyAuthoring` COMPONENT** (not a GameObject). Scope: authoring track/clips in a
`.playable`, wiring a SubScene PlayableDirector, the runtime rotation-motor semantics. Physics
bodies, Targets slots, and stat setups are OTHER specialists' domains (report a missing prerequisite,
never improvise). **Family patterns live in `unity-track-physics-filter-override`** (two-system
split, producer/modifier groups, central `PhysicsTimelineBakingSystem`, timeline-activation scope,
silence profile) — cite, don't re-derive. **This skill carries the SHARED PID CORE** consumed by
`unity-track-physics-linear-pid` (which differs ONLY in: error = position delta, its own
`PidLinearTargetMode` incl. `InitialLocal`/`LineOfSight`, `TargetOffset` float3 instead of
`TargetRotation`, and `PendingForce.Linear` instead of `.Angular`).

**Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor
per `unity-cli`.** That shared skill owns the discovery preamble (§1), the SubScene bracket (§2),
the undo appendix (§3), and the verification protocol (§4); this skill keeps ONLY its track-unique
facts below. For discovery use §1, the create-and-wire bracket §2, undo §3, verification §4 there —
with the track-specific deltas noted inline below.

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

### Type facts
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

## 3. DISCOVERY (per unity-timeline-track-authoring §1)

Run §1 D1–D5. Track-specific deltas:
- **D1 package check** — `BovineLabs.Timeline.Physics.Authoring.PIDs.PhysicsAngularPIDTrack,
  BovineLabs.Timeline.Physics.Authoring`.
- **D4 bind target** — `Unity.Physics.Authoring.PhysicsBodyAuthoring` COMPONENT (not a Transform).
  Print MotionType/Mass + sibling TargetsAuthoring/StatAuthoring per body. Prerequisites:
  MatchTarget/LookAtTarget/Flee/MatchTargetOpposite need the BODY's Targets slot populated (a lost
  slot triggers the §2 silent fallback); World needs nothing. `strengthStat` needs a stat schema
  asset (`AssetDatabase.FindAssets("t:StatSchemaObject")`, print each name +
  `System.Convert.ToInt64(schema.Key)` — keys drift; verify StatAuthoring + a StatDefaults entry
  for that key). Missing setup = report the gap, don't improvise it.

## 4. CANONICAL RECIPES (per unity-timeline-track-authoring §2)

Use the §2 bracket; track type = `PhysicsAngularPIDTrack`, clip type = `PhysicsAngularPIDClip`,
bind target = `PhysicsBodyAuthoring` COMPONENT. Clip fields are camelCase serialized — set via
`SerializedObject` (nested tuning = `tuning.Proportional` etc.). The track-specific MIDDLE replaces
§2's clip block with one of these designer-intent → wiring patterns:

- **Settle at an absolute world rotation** (no Targets needed): `targetMode=World(2)`,
  `trackingTarget=None(0)`, `targetRotationEuler=(0,90,0)` DEGREES.
- **Face the target**: `targetMode=LookAtTarget(1)`, `trackingTarget=Target(1)` (Targets slot must
  point at it — TargetsAuthoring, or retarget mid-timeline with EntityLinkTargetPatch ORDERED
  before this track). Start Balanced (P10 D3 I1 Max100), raise P for urgency then D to kill wobble;
  `strength=2` doubles output.
- **Ride alignment / face-off**: `MatchTarget(0)` + `trackingTarget=Target`; `targetRotationEuler`
  as a deliberate offset if the meshes' forward axes disagree; `MatchTargetOpposite(4)` = 180°-yawed.
- **Cower**: `FleeFromTarget(3)` — look-at the mirror point `self + (self − target)`; same
  up-righting caveat as LookAt.
- **Stat-scaled turning**: `strengthStat=<discovered schema>` + `readStatFrom` at an entity
  ACTUALLY carrying a Stat buffer WITH that key (×100 — author 200 for double); avoid
  `readStatFrom=Self` unless the body has stats.
- **Ending crisp**: a PID clip ending leaves spin — VelocityClamp, or accept the handoff.

## 5. WORKED EXAMPLE DELTA (vs unity-timeline-track-authoring §5 — rediscover, never assume)

Bind target is **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** (the program's first
non-GameObject physics binding; Dynamic, Mass=1, `Targets.Target=Stage_Target` via TargetsAuthoring,
**NO StatAuthoring** — the stat-trap layer-1 exhibit). Asset
`Assets/Training/18-physics-angular-pid-track/AngularPIDMastery.playable`, one track
`AngularPIDTrack`, clips: `A_World90Yaw` 0–3 World euler=(0,90,0) tracking=None /
`B_LookAtTarget` 4–7 LookAtTarget tracking=Target strength=2 / `C_StatDriven` 8–10 MatchTarget
tracking=Target strengthStat=SlowMo readStatFrom=Self (stat-less ball → silently ×1). Director table
after the lesson: **16** entries, #16 = `AngularPIDTrack (PhysicsAngularPIDTrack) → Stage_PhysicsBall
(PhysicsBodyAuthoring)`, prior 15 intact; director restored to
`Assets/Training/01-transform-position-track/PositionMastery.playable`. vex-ee stat schema: SlowMo,
`key=94`, guid 95a61e8c263e49dbbc6a31729d0815ce, stage default Added=25 → reads ×0.25.

## 6. UNDO + VERIFICATION

Undo per **unity-timeline-track-authoring §3** (artifact inventory 1–4, restore-director-FIRST
order, UNDO-1/2/3/4 templates) — runtime effects (spin, PID state) exist only in play mode, nothing
to undo there; no extras beyond the standard asset+folder+director+binding artifacts.

Verify per **unity-timeline-track-authoring §4**. Track-specific read-back expectations:
- Asset dump fields: `targetMode`, `trackingTarget`, `targetRotationEuler`, `strength`, `tuning`,
  `strengthStat`, `readStatFrom`. Expect `caps=Blending|Looping`; default tuning P=(10,10,10)
  D=(1,1,1) I=(2,2,2) Max=100.
- YAML: enums as ints, `targetRotationEuler` in DEGREES (radians are bake-only), tuning as nested
  float3 blocks, `strengthStat: {fileID: 0}` when null vs asset→asset ref, `m_BlendInDuration: -1`
  when no overlap authored.
- Prerequisite re-check: bound body's MotionType/Mass; its Targets slot if any clip uses a
  target-relative mode (the §2 fallback is silent); StatAuthoring + key if `strengthStat` is set.
- Binding: `BIND|<i>|<trackName> (PhysicsAngularPIDTrack) -> <bodyGoName> (PhysicsBodyAuthoring)`.
  Silence is expected, not evidence (family pattern 7).
