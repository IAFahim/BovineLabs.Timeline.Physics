---
name: unity-track-physics-drag
description: Master of PhysicsDragTrack + PhysicsDragClip (package BovineLabs.Timeline.Physics) — stateless while-timeline-active exponential velocity decay, the honest instant-stop math, the brakes-off stat trap. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "air-brake / spin down / thicken the air during this clip".
---

# PhysicsDragTrack specialist
## 1. SCOPE

You are the specialist for **`PhysicsDragTrack`** ("BovineLabs/Physics/Drag (Brakes)") and
**`PhysicsDragClip`** from the package `BovineLabs.Timeline.Physics`, ns
`BovineLabs.Timeline.Physics.Authoring` — bound to a **`PhysicsBodyAuthoring` COMPONENT**. Scope:
authoring track/clips in a `.playable`, wiring a SubScene PlayableDirector, the runtime decay
semantics. Physics bodies and stat setups are OTHER specialists' domains (protocol §6: report a
missing prerequisite, never improvise). **Family patterns 1–7 live in
`unity-track-physics-filter-override`**; **the stat chain (`StatStrengthUtility.Resolve`, the triple
trap) is banked in `unity-track-physics-angular-pid`** — cite both, don't re-derive. This skill owns
the decay math, the no-State analysis, and the drag edition of the stat trap (×0 = brakes OFF, the
inverse of the PID's dead motor).

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor per
`unity-cli`. Discovery preamble → that skill's §1; the SubScene bracket → §2; undo appendix → §3;
verification protocol → §4. This skill supplies only the placeholders those sections name: the type
facts, field table, runtime semantics, traps, and the drag-specific clip patterns below.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say where a fact was
PROVEN, not where it applies. (All verified vex-ee 2026-06 via reflection dumps, package-source +
raw YAML reads, fresh-load read-backs, live Unity.Mathematics computation through `unity-cli
exec`; no play mode — source-derived.)

While `ActiveDrag` is enabled, every fixed step multiplies `PhysicsVelocity.Linear/.Angular` IN
PLACE by `exp(-drag × multiplier × dt)` — pure attenuation of whatever velocity the body currently
has. The family's SIMPLEST track and its **stateless exception**: no State component, no Fired
machine, no capture, no restore. Runtime effects exist only in play mode.

| Type | Facts |
|---|---|
| `PhysicsDragTrack` | ns `BovineLabs.Timeline.Physics.Authoring`, base `DOTSTrack`, **NOT sealed** (unlike the sealed Filter/PID types — cosmetic, but matters when reflecting). `[TrackClipType(PhysicsDragClip)]`, `[TrackBindingType(typeof(Unity.Physics.Authoring.PhysicsBodyAuthoring))]`, `[TrackColor]`, `[DisplayName("BovineLabs/Physics/Drag (Brakes)")]`. |
| `PhysicsDragClip` | base `DOTSClip`, `ITimelineClipAsset`, `clipCaps => ClipCaps.Blending \| ClipCaps.Looping` (REAL mixer), `duration => 1` (seed only). Bake: unconditional, SILENT (family pattern 7) — adds `PhysicsDragAnimated{AuthoredData}` to the clip entity via `PhysicsDragBuilder`. `PhysicsDragData` = `float Linear; float Angular; StatStrengthConfig Strength;`; `PhysicsDragAnimated` = `IAnimatedComponent<PhysicsDragData>` + `IPreparable` (CLIP entity). |
| `ActiveDrag` | `IActive<PhysicsDragData>` — `Config` only. **No companion State component** (contrast `PhysicsFilterOverrideState`, `PhysicsAngularPIDState`). |
| Systems | `PhysicsDragTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`) = pure `TrackBlendDriver<PhysicsDragData, PhysicsDragAnimated, ActiveDrag, PhysicsDragMixer>`; `PhysicsDragApplySystem` (`PhysicsModifierGroup`, `[UpdateBefore(PhysicsVelocityOverrideSystem)]` — AFTER the physics step, before velocity overrides) hosts `ApplyDragJob`. |
| Extras | The package ships Drag DEBUG systems (`DragDebugSystem`, `PhysicsDragGizmoSystem`) and a real test class (`PhysicsDragApplySystemTests`) — the only Physics-family track so far with its own dedicated test file. |

### Clip fields — camelCase serialized names, defaults from a fresh instance (reflection)
| Field | Type | Default | Meaning |
|---|---|---|---|
| `linearDrag` | float | `5` | Tooltip verbatim: "Linear drag multiplier. 0 = no drag. 50 = instant stop (at 50hz)." **No `[Min]`** — negatives accepted. |
| `angularDrag` | float | `5` | Tooltip verbatim: "Angular drag multiplier. 0 = no drag. 50 = instant stop (at 50hz)." No `[Min]`. |
| `strengthStat` / `readStatFrom` / `readStatLink` | StatSchemaObject / Target / EntityLinkSchema | `null` / `Self(4)` / `null` | Optional ×100-fixed-point stat MULTIPLIER (shared `StatStrengthConfig`) — multiplies BOTH drags inside the exponent; whose stat buffer (via the BODY's Targets); optional link override. |

YAML: plain float lines per drag, `readStatFrom` as int, `strengthStat: {fileID: 11400000, guid:
..., type: 2}` when assigned (asset→asset ref), overlap auto-creates
`m_BlendOutDuration`/`m_BlendInDuration` pairs (Blending caps).

### Runtime source (quoted)

`PhysicsDragApplySystem.ApplyDragJob` — the whole consumer:

```csharp
var dt = SystemAPI.Time.DeltaTime;
if (dt <= 0.0001f) return;                          // dt early-out (system level)
...
var multiplier = StatStrengthUtility.Resolve(in config.Strength, entity, targets, LinkSources, Links, StatLookup);
multiplier = math.max(0f, multiplier);              // stat can never flip the sign
if (multiplier <= 0.00001f) continue;               // multiplier 0 => body SKIPPED (drag off)
PhysicsMath.ComputeExponentialDecay(facet.Velocity.ValueRO, config, DeltaTime, multiplier, out var vOut);
facet.Velocity.ValueRW = vOut;                      // DIRECT in-place PhysicsVelocity write
```

`PhysicsMath.ComputeExponentialDecay` verbatim — the entire effect: `velocityOut = velocityIn;
if (deltaTime <= 0f) return; velocityOut.Linear *= math.exp(-drag.Linear * multiplier *
deltaTime); velocityOut.Angular *= math.exp(-drag.Angular * multiplier * deltaTime);`

`PhysicsDragMixer` verbatim:

```csharp
public PhysicsDragData Lerp(in PhysicsDragData a, in PhysicsDragData b, in float s)
    => new PhysicsDragData { Linear = math.lerp(a.Linear, b.Linear, s), Angular = math.lerp(a.Angular, b.Angular, s),
        Strength = s < 0.5f ? a.Strength : b.Strength };   // threshold copy
public PhysicsDragData Add(in PhysicsDragData a, in PhysicsDragData b)
    => new PhysicsDragData { Linear = a.Linear + b.Linear, Angular = a.Angular + b.Angular, // drags SUM
        Strength = a.Strength };                            // FIRST operand's stat config wins
```

Note the Add asymmetry: drags sum but `Strength = a.Strength` — under Add the first-accumulated
clip's stat config silently governs both clips' summed drag. A lerped crossfade genuinely passes
through a both-drags-active middle; at exactly s=0.5 the LATER clip's Strength wins.

### THE STATELESSNESS ANALYSIS (this skill's headline)

The no-State evidence — three layers: (1) type sweep over all loaded assemblies —
`STATE_SWEEP|*DragState* count=0`; (2) the apply job — no Fired/enter/stay/exit branches, no
capture fields (quoted above, the whole consumer); (3) the central baker's Drag section — the
binding entity gets **ActiveDrag only, no State** (`AddComponent<ActiveDrag>` +
`SetComponentEnabled(false)` + `EnsureAccumulationBuffers`), where every sibling gets an
Active+State pair. Why no State is *needed*: exponential decay is a pure function of the CURRENT
velocity — `v *= exp(-k·dt)` never references "what the velocity was before the clip started".
Attenuation, not replacement; at timeline end the bit goes off and the body keeps whatever
velocity remains — the physically-correct handoff.

**What no-State BUYS:** **scrub-proof** (re-entering a clip mid-run re-applies the same pure
function; no enter branch to fire twice, no snapshot to corrupt); **no capture poisoning** (the
family's nastiest trap — pattern 5, a restore=false run ends and the next run captures the mutated
state as "original" — is structurally impossible: Drag has no original); **no cross-track restore
conflicts** (the Kinematic×Gravity exit-clobbers-restore bite cannot involve Drag — no exit write;
Drag composes with any sibling, attenuating whatever velocity they produced this step —
PhysicsModifierGroup, after the step, before VelocityOverride, so a same-step VelocityOverride
re-imposes its velocity AFTER drag); **no restore-flag semantics** (no `restoreOnExit` field — the
"LAST clip's flag decides for the whole run" trap of Filter/Gravity has no Drag form).

**What no-State COSTS:** **no restore — permanent velocity loss** (velocity lost to drag is gone,
no "undo the braking" any more than for real air; want the speed back → a Force/Velocity clip);
**gap bleed** (the track system is the stock `TrackBlendDriver` kernel, so the family gap trap
holds verbatim — `ActiveDrag` stays enabled from the first clip to timeline deactivation, gaps
hold the last blended config at full weight; "stateless" still has timeline-activation-scoped
*behavior*, just no stored *data*); **no per-clip identity** (only the single blended Config
exists at apply time — no per-clip cleanup or attribution, contrast EssenceStat's
remove-by-SourceEntity).

### Edge cases & traps (each source-proven or computed live, vex-ee 2026-06)

- **DON'T author negative drag — unguarded exponential runaway** — no `[Min]` on the fields;
  authored `-5` gives `exp(+5·dt)` growth: ×1.105171 per 50 Hz step, **×148.4 after 1s, ×3.27e6
  after 3s** (computed live). The stat clamp `math.max(0f, multiplier)` only stops a negative STAT
  flipping a positive drag — a positive multiplier preserves an authored negative exponent, so the
  stat path CANNOT rescue it. Use a Force clip for boosts.
- **DON'T read "50 = instant stop (at 50hz)" literally** — `exp(-50 × 0.02) = 0.367880`: one step
  removes **63.2%**, not 100%; 90% of velocity gone in 46.1 ms, 99% in 92.1 ms, ~1.9e-22 after a
  full second — asymptotic decay, exact zero never reached; the tooltip is a perceptual
  approximation ("visually stopped within ~2–5 fixed steps").
- **DON'T expect drag to replace or disable built-in PhysicsDamping — they COMPOUND** — grep of
  every .cs in the package for `PhysicsDamping`: **0 hits**; Unity.Physics integrates its own
  damping every step regardless — nonzero Linear/AngularDamping brakes harder than the clip alone.
- **DO pick drag vs VelocityClamp deliberately** — drag is proportional decay toward zero (smooth,
  never quite arrives, scales with speed); VelocityClamp is a hard ceiling (no effect below the
  cap, instant truncation above). "Thicker air / fading momentum" → Drag; "never faster than X" →
  VelocityClamp; "kill residual PID spin at a cut" → clamp (or kinematic freeze), because drag's
  asymptote leaves a remainder.
- **DON'T trust the stat multiplier without walking the triple trap — drag edition** (chain banked
  in `unity-track-physics-angular-pid`): (1) no buffer/unresolvable → ×1 (a stat clip on a
  stat-less body = a plain brake); (2) buffer present → ×100-decoded (authored 25 = ×0.25 braking,
  not ×25); (3) buffer present, KEY absent → `GetValueFloat` 0 → the `continue` skip → **brakes
  silently OFF** — the body sails on undamped. Same root cause as the PID's ×0, OPPOSITE symptom:
  PID = "my motor is dead", drag = "my brakes failed". Triage by the buffer's KEYS, not presence.
- **DO know multiplier-zero SKIPS, not zero-drags** — `if (multiplier <= 0.00001f) continue;` — a
  stat-zeroed drag never computes `exp(0)`; the body is skipped entirely. Same observable (no
  braking), cheaper path — "stat=0 disables the brakes" is an intended off-switch shape.
- **DON'T treat gaps as neutral — the body keeps braking** — stock `TrackBlendDriver` kernel +
  `DisableStaleTrackJob` disabling only at TIMELINE deactivation (family pattern 5): gaps hold the
  last blended config at full weight.
- **DO expect Add to brake HARDER and keep the FIRST stat config** — `a.Linear+b.Linear` /
  `a.Angular+b.Angular` summed inside the exponent (two full-weight clips outbrake either alone),
  `Strength = a.Strength`. Contrast the lerped crossfade's weighted middle.
- **DO use `Convert.ToInt64(Key)` when printing `StatSchemaObject.Key` in exec snippets** — `Key`
  is a plain value; `.Key.Value` fails to compile.
- **DO note the silence profile (family rule 7)** — Bake unconditional, no guards; unbound track →
  central-baker `Entity.Null` continue = total silent no-op. Clean console proves nothing.

### Drag-specific prerequisite note (resolve per authoring §1.D4)

The bind target is a `PhysicsBodyAuthoring` COMPONENT — when discovering it, also dump
MotionType, Mass, and **Linear/AngularDamping** (built-in PhysicsDamping COMPOUNDS with drag, see
traps). `strengthStat` needs a `StatSchemaObject` AND the bound body to carry `StatAuthoring` with
that key in `StatDefaults`, else the silent layers above fire (layer 3 turns the brakes OFF). Keys
drift between projects — re-read, never assume.

## 3. CLIP PATTERNS (the bracket's track-specific middle — authoring §2)

Designer intent → wiring. Values are choices, NOT package constants. Set fields via
`SerializedObject` using the camelCase YAML names in §2.

- **AIR-BRAKE — kill linear speed, keep the spin.** One clip; `linearDrag=20` (~visually stopped
  in ~0.2s at 50 Hz), `angularDrag=0`.
- **SPIN-DOWN — kill the spin, keep the flight** (the classic post-PID cleanup: an Angular PID clip
  ends with momentum and no restore; follow it with a drag clip on the same body). `linearDrag=0`,
  `angularDrag` 20–40.
- **THICK AIR / underwater zone.** One long clip spanning the regime, moderate both-drags (3–8);
  built-in PhysicsDamping still applies on top.
- **STAT-SCALED BRAKES.** `strengthStat` + `readStatFrom=Self` — ensure the BOUND BODY actually has
  StatAuthoring with that key in StatDefaults (×100: author 100 for ×1). Missing buffer = brake at
  ×1; present-buffer-missing-key = brakes OFF (silent).
- **CROSSFADE brake→spin-down.** Overlap two clips (`blendInDuration` on the later; Blending caps) —
  the mixer lerps both drags through a both-axes middle. Stack same-time full-weight clips only when
  you WANT summed (harder, Add-path) braking. **Never** author negative drag — exponential energy
  injection, ×148/s at −5; the stat clamp cannot save you. Timeline scope: the brake keeps dragging
  through gaps until timeline end.

## 4. WORKED EXAMPLE DELTA (vs unity-timeline-track-authoring §5 — rediscover, never assume)

On the shared vex-ee stage, bound to **`Stage_PhysicsBall`'s PhysicsBodyAuthoring COMPONENT** (pos
(0,1,5), Dynamic, Mass=1, **LinearDamping=0.01, AngularDamping=0.05** — the compound-damping
exhibit is live; **NO StatAuthoring**, keeping the triple-trap layer-1 exhibit live). Asset
`Assets/Training/20-physics-drag-track/DragMastery.playable`, one track `DragTrack`, clips:
`A_AirBrake` 0–2 linearDrag=20 angularDrag=0 blendOut=0.5 / `B_SpinDown` 1.5–3.5 linearDrag=0
angularDrag=30 blendIn=0.5 / `C_StatScaledBrake` 5–6 linearDrag=10 angularDrag=10
strengthStat=SlowMo readStatFrom=Self (on the stat-less ball = plain 10/10 brake, layer 1). Gap
exhibit: the ball keeps braking at B's full angular=30 through the 3.5–5s gap. Mid-overlap hand
math at t=1.75s, s=0.5: Linear=lerp(20,0,0.5)=10, Angular=lerp(0,30,0.5)=15, Strength
threshold-copies B's. vex-ee stat schema: SlowMo, `key=94`, guid 95a61e8c263e49dbbc6a31729d0815ce.
Director table grew to 18 entries (#18 = `DragTrack (PhysicsDragTrack) → Stage_PhysicsBall
(PhysicsBodyAuthoring)`), prior 17 intact; restored to its PRE value
`Assets/Training/01-transform-position-track/PositionMastery.playable` (bindingCount=17).
