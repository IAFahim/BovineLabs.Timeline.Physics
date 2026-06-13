---
name: unity-track-physics-gravity-override
description: Master of PhysicsGravityOverrideTrack + PhysicsGravityOverrideClip (package BovineLabs.Timeline.Physics) — blended while-timeline-active gravity scaling via PhysicsGravityFactor add/mutate, the add-path one-step latency, capture poisoning. Portable to any project containing the package; worked example from vex-ee.
---

# PhysicsGravityOverrideTrack specialist

## 1. SCOPE

You are the specialist for **`PhysicsGravityOverrideTrack`** and
**`PhysicsGravityOverrideClip`** from `BovineLabs.Timeline.Physics`
(namespace `BovineLabs.Timeline.Physics.Authoring.Gravities`). Scope: exactly
this track family — authoring the track/clips in a `.playable`, wiring a SubScene
PlayableDirector, and the runtime gravity-override semantics. A missing physics
body is a missing-prerequisite report, never something you create. Family
patterns live in `unity-track-physics-filter-override` (its **PHYSICS FAMILY
SHARED PATTERNS** section — cite it, don't re-derive); kinematic freezing in
`unity-track-physics-kinematic-override` (see its CONFLICT MATRIX before ever
sharing a body with a kinematic track).

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`;
use the editor per `unity-cli`.

## 2. TYPE FACTS

All verified vex-ee 2026-06 via reflection dumps, package-source `File.ReadAllText`
reads inside exec, raw YAML reads, fresh-load read-backs through `unity-cli exec`;
no play mode (runtime claims are source-derived). Provenance tags say where a fact
was PROVEN, not where it applies — these are true in ANY project with the package.

| Type | Facts |
|---|---|
| `PhysicsGravityOverrideTrack` | `...Physics.Authoring.Gravities`, asm `BovineLabs.Timeline.Physics.Authoring`, sealed, EMPTY body, base `DOTSTrack`. `[TrackClipType(typeof(PhysicsGravityOverrideClip))]`, **`[TrackBindingType(typeof(GameObject))]`** (bind the GameObject ITSELF, not a component), `[TrackColor(0.2,0.6,0.8)]`, `[DisplayName("BovineLabs/Physics/Gravity Override")]`. |
| `PhysicsGravityOverrideClip` | sealed, base `DOTSClip`, `ITimelineClipAsset`, **`clipCaps => ClipCaps.Blending`** (contrast Filter's None), `duration => 1` (seed only). |
| `PhysicsGravityOverrideData` | `...Physics`, asm `...Physics.Data`, IComponentData: `float GravityScale; bool RestoreOnExit;` |
| `PhysicsGravityOverrideAnimated` | `IAnimatedComponent<PhysicsGravityOverrideData>` — `AuthoredData` + `Value`, on the CLIP entity. |
| `ActiveGravityOverride` | IComponentData + **IEnableableComponent**: `PhysicsGravityOverrideData Config` — on the BINDING entity, added DISABLED at bake. |
| `PhysicsGravityOverrideState` | IComponentData: `bool Fired; float OriginalGravityScale; bool AddedComponent;` — on the BINDING entity. |
| `PhysicsGravityOverrideMixer` | `IMixer<PhysicsGravityOverrideData>` — REAL math: `GravityScale = math.lerp(a.GravityScale, b.GravityScale, s)`, `RestoreOnExit = s >= 0.5f ? b.RestoreOnExit : a.RestoreOnExit`; `Add(a,b) => b`. |
| `Unity.Physics.PhysicsGravityFactor` | `float Value` — the component the apply system adds/mutates/removes. |
| Systems | `PhysicsGravityOverrideTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`, per rendered frame) produces the enabled `ActiveGravityOverride{Config}` (enable via the **BeginSimulation** ECB — effective next frame); `PhysicsGravityOverrideApplySystem` (**`PhysicsProducerGroup`** = FixedStep BEFORE `PhysicsSystemGroup`; query uses `IgnoreComponentEnabledState`) consumes it against `PhysicsGravityFactor`. |

**Field table** (camelCase, fresh-instance reflection):

| Field | Type | Default | Meaning |
|---|---|---|---|
| `gravityScale` | float | `1` | gravity multiplier — 1=normal, 0=zero-G, negative=reversed; **UNCLAMPED** (no clamp in clip, mixer, or apply system). |
| `restoreOnExit` | bool | `True` | restore/remove at override-REGIME end (timeline DEACTIVATION, NOT clip end). |

`DEFAULTS|gravityScale=1|restoreOnExit=True|duration=1|clipCaps=Blending`.

**Distinctions from the Filter sibling:**
1. **REAL blending** — `ClipCaps.Blending`: every clip bakes a `ClipWeight`,
   overlaps genuinely lerp through `PhysicsGravityOverrideMixer` (Filter's
   `ClipCaps.None` = first-writer races, dead-code mixer).
2. **PhysicsProducerGroup** — the apply system FEEDS the simulation (runs BEFORE
   `PhysicsSystemGroup`; Filter's runs after, in the modifier group).
3. **Component add/mutate/remove** — plain IComponentData, not a blob mutation;
   **NO ForceUnique requirement**, no shared-blob warning — but the ADD path is
   one fixed step latent.

The deciding bake line, quoted from the timeline package's `ClipBaker.cs`:
`if ((clip.clipCaps & ClipCaps.Blending) != 0) { context.Baker.AddComponent(clipEntity, new ClipWeight { Value = 1 }); }`

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
(always baked factor 0) → **MUTATE** in place (same-tick). Predict this from §1
discovery of the bound body and record it in your card.

## 3. RUNTIME SEMANTICS

Bake (quoted from `PhysicsGravityOverrideClip.Bake`): unconditional — no guard,
no LogError; adds `PhysicsGravityOverrideAnimated{AuthoredData}` via
`PhysicsGravityOverrideBuilder.ApplyTo`, then `base.Bake`. **NO bake-time failure
mode.** Binding-entity pair from the central `PhysicsTimelineBakingSystem`
(identical family shape to Filter's quoted loop with Gravity types substituted;
same `Entity.Null` continue — unbound track = total silent no-op).

The track system runs the family kernel per rendered frame (filter skill, patterns
4–5; `ResetStateTrackJob` resets State to `{Fired=false, AddedComponent=false,
OriginalGravityScale=1}` only while `ActiveGravityOverride` is disabled) — but
every gravity clip bakes a `ClipWeight`, so overlaps go through the 4-slot weighted
register and `WriteActiveJob` writes `Config = JobHelpers.Blend<...Data, ...Mixer>(
ref mixData, default)` — a genuine weighted lerp — then ECB-enables
`ActiveGravityOverride` via the BeginSimulation ECB; `DisableStaleTrackJob` disables
only on the timeline-deactivation edge.

`PhysicsGravityOverrideApplySystem` runs the Fired machine per fixed step:
- **enter** — component present → capture `OriginalGravityScale`, write
  `Value = Config.GravityScale` in place, `AddedComponent=false`; absent →
  `OriginalGravityScale=1`, `AddedComponent=true`, `ECB.AddComponent(chunkIndex,
  entity, new PhysicsGravityFactor{Value})` on the **EndFixedStepSimulation** ECB.
- **stay** — re-assert in place, or re-add via ECB if (externally) missing.
- **exit** (timeline deactivation, last-written Config) — `RestoreOnExit` →
  `AddedComponent ? ECB.RemoveComponent<PhysicsGravityFactor>` : in-place restore
  of the captured original (third branch re-ADDS the original if the component
  vanished mid-run); `RestoreOnExit=false` → component and value left permanently.

**Mental model:** same regime shape as Filter (family pattern 5) — first gravity
clip enters at its first frame, gaps hold the last blended Config (stay keeps
re-asserting), exit fires once at timeline deactivation with the LAST clip's
restore flag deciding for the whole run — but the target is a plain IComponentData
add/mutate/remove, not a blob, so no ForceUnique requirement, and overlapping clips
genuinely lerp instead of racing.

**Silence profile (family rule 7 holds):** bake unconditional; unbound track =
`Entity.Null` continue = total silent no-op; runtime has NO loud failure at all (no
blob, no ForceUnique, no analogue of Filter's shared-blob warning). A clean console
proves nothing.

### Traps & DO/DON'T (each source-proven, vex-ee 2026-06)

- **DO know which path your body takes — add vs mutate, exact-archetype recovery
  on remove** — quoted `OnEnter`: `hasGravityFactor` → capture
  `gravityFactors[i].Value`, `AddedComponent=false`, in-place write (same-tick);
  else → `OriginalGravityScale=1`, `AddedComponent=true`, `ECB.AddComponent(...)`
  (one-step latent). Exit symmetry: `AddedComponent ?
  ECB.RemoveComponent<PhysicsGravityFactor>() :` in-place restore. An add-path body
  returns to its exact baked archetype on restore.
- **DON'T file the stay-path duplicate-add as a bug — adjudicated NOT A BUG** —
  (1) the apply system queues on `EndFixedStepSimulationEntityCommandBufferSystem`
  (`OrderLast = true`) — commands queued during fixed step N play back at the END
  of step N, so the "absent window" closes before the apply system's next update;
  (2) enter and stay are exclusive branches of one per-entity dispatch — the stay
  re-add is reachable only if an external actor removes the component mid-regime:
  deliberate self-healing (mirroring the exit path's re-add-original third branch);
  (3) with-value `EntityCommandBuffer.AddComponent<T>` is documented *"At playback,
  if the entity already has this type of component, the value will just be set"* —
  idempotent, never a throw.
- **DO expect the REAL quirk instead: one-fixed-step latency on the ADD path** —
  the apply system runs in `PhysicsProducerGroup` (before the step), but the
  EndFixedStep ECB plays back AFTER it — on the enter tick of a component-less
  body, that tick's physics step still integrates with implicit factor 1. The
  mutate path writes in place before the step: effective the same tick. Asymmetric
  first-tick behavior; negligible at 60Hz, but real.
- **DO overlap gravity clips — same family as Filter, OPPOSITE overlap rule** —
  the ClipBaker line adds `ClipWeight` only when caps include Blending, so gravity
  clips take the 4-slot weight-sorted register and `PhysicsGravityOverrideMixer.Lerp`
  actually runs (at a 50/50 point, `math.lerp(a, b, 0.5)`). `RestoreOnExit` blends
  discretely (`s >= 0.5f ? b : a`). >4 simultaneous gravity clips on one binding:
  lowest weight silently dropped.
- **DO use negative/extreme scales freely — UNCLAMPED everywhere** — negative
  values serialize verbatim (raw YAML verified). It scales the world gravity
  vector, it does not replace velocity: a falling body under scale −1 decelerates,
  then rises.
- **DON'T expect normal gravity in gaps — gaps hold the last blended value, not
  neutral** — `DisableStaleTrackJob` keys off `TimelineActivePrevious &&
  !TimelineActive`, never clip end; stay re-asserts the last Config every fixed step
  through every gap.
- **DON'T let a restoreOnExit=false clip near a body you ever want back —
  timeline-end restore + capture poisoning** — exit runs ONCE with the LAST clip's
  Config; `RestoreOnExit=false` leaves component and value permanently. **Next
  run**: the component now EXISTS → run 2 takes the MUTATE path — enter captures the
  mutated value as `OriginalGravityScale`, `AddedComponent=false`; even all-restore=
  true clips now "restore" the mutated value, and remove never fires. Undo needs a
  compensating clip or external `RemoveComponent`. (Within one run capture stays
  safe: Fired=true blocks re-capture; ResetStateTrackJob is gated on Active being
  disabled.)
- **DON'T be surprised by a component the authoring never had — bake/runtime
  divergence** — restoreOnExit=false leaves a PERMANENT `PhysicsGravityFactor` on an
  entity whose baked archetype never authored one; "why does this body have gravity
  factor 0.5? The PhysicsBodyAuthoring says 1" has no authoring-side answer — the
  timeline did it. Also flips the next run's branch from add to mutate.
- **DON'T share a body with a PhysicsKinematicOverrideTrack in the same timeline** —
  `zeroGravity` writes the SAME component; see the kinematic skill's CONFLICT MATRIX
  (orphaned adds, config-blind exit clobbers).

## 4. DISCOVERY DELTA (per unity-timeline-track-authoring §1)

Run the five openers there with TRACK_FULLNAME =
`BovineLabs.Timeline.Physics.Authoring.Gravities.PhysicsGravityOverrideTrack`,
TRACK_ASSEMBLY = `BovineLabs.Timeline.Physics.Authoring`, PACKAGE =
`BovineLabs.Timeline.Physics`. Track-specific notes:

- **Bind target is a GameObject, not a component** (`[TrackBindingType(GameObject)]`).
  The D4 "find target by component" step finds the **physics body** by its
  authoring component (`Unity.Physics.Authoring.PhysicsBodyAuthoring` — sweep
  assemblies for the `com.unity.physics` vs `.custom` variant), but you BIND the
  GameObject itself. Per body print: hierarchy path, scene.path, MotionType, Mass,
  GravityFactor. ZERO bodies → missing prerequisite ("no PhysicsBodyAuthoring in the
  SubScene — a physics-stage specialist must add one; I override gravity, I don't
  create bodies"). Several → confirm with designer.
- **Path prediction (record in card):** Dynamic + authored GravityFactor=1 → ADD
  path (one-step latency, exact-archetype restore); GravityFactor≠1 or Kinematic →
  MUTATE path (same-tick, in-place restore).
- **Cross-track check:** if any timeline on this body carries a
  `PhysicsKinematicOverrideTrack`, surface the conflict matrix before proceeding.

## 5. CLIP PATTERNS (the bracket's track-specific middle — per §2 of the shared skill)

Build inside the shared SubScene bracket; create the track with
`timeline.CreateTrack<...Gravities.PhysicsGravityOverrideTrack>(null, trackName)`,
then `SetGenericBinding(track, bodyGo)` with the **GameObject ITSELF**, not a
component. Set fields by direct assignment after casting `clip.asset`:
`((...Gravities.PhysicsGravityOverrideClip)clip.asset).gravityScale = ...;`.
Starts/durations/scales below are example choices, not package constants.

- **ZERO-G** — designer "weightless during this window": one clip, `gravityScale=0`,
  `restoreOnExit=true`. Restores at timeline end (regime end), not clip end.
- **REVERSE + BLEND** — "glide from one gravity regime into another": a second clip
  (`gravityScale=-1`, `restoreOnExit=true`) overlapping the first, with
  `clipB.blendInDuration` set (mirrored computed blendOut on the earlier clip).
  Overlaps genuinely lerp (this track blends; its Filter sibling races) — at a 50/50
  point `math.lerp(g_a, g_b, 0.5)`.
- **PERMANENT (e.g. moon gravity 0.5)** — "leave it changed": `restoreOnExit=false`
  on the LAST clip. WARN the designer: permanent `PhysicsGravityFactor` component +
  next-run capture poisoning (trap in §3); pair with a compensating clip when it
  must be temporary.

## 6. UNDO (per unity-timeline-track-authoring §3)

Authoring-artifacts journal per the shared §3 structure (UNDO-1 restore director
first, UNDO-2 delete asset, UNDO-3 other captured values, UNDO-4 fresh-load verify).
Track-specific notes:

- This track's runtime effect (the `PhysicsGravityFactor` add/mutate, regime state,
  capture poisoning) exists ONLY in play mode and is never serialized — the journal
  covers AUTHORING artifacts only. The permanence/poisoning hazard is a designer
  warning (§3 trap / §5 PERMANENT pattern), NOT a journal entry.
- This recipe never touches the body's authoring components — UNDO-3 is normally
  empty beyond UNDO-1.

## 7. VERIFICATION DELTA (per unity-timeline-track-authoring §4)

Run the shared §4 protocol. Track-specific expectations:

- **Asset dump (step 1):** every clip `caps=Blending`; dump `gravityScale` /
  `restoreOnExit` per clip.
- **Raw YAML (step 2):** `gravityScale` plain floats, `restoreOnExit` as 1/0; REAL
  blend YAML on any overlap (`m_BlendOutDuration` earlier clip, `m_BlendInDuration`
  later, populated mix curves).
- **Prerequisite re-check (step 3):** fresh-load dump of the bound body —
  MotionType/Mass/GravityFactor. Factor 1 + Dynamic ⇒ ADD path; ≠1 or Kinematic ⇒
  MUTATE. Record which in your card.
- **Binding (step 4):** expect `BINDING|<trackName>|bound=<bodyGoName>
  (UnityEngine.GameObject)` — the GameObject verbatim, all prior entries intact.
- **Console (step 6):** silent even when misconfigured — silence is expected, not
  evidence (family pattern 7).

## 8. WORKED EXAMPLE DELTA (vex-ee — rediscover, never assume)

Beyond the shared §5 stage: asset
`Assets/Training/16-physics-gravity-override-track/GravityOverrideMastery.playable`,
one track `GravityTrack` bound to **Stage_PhysicsBall** (the GameObject) — dynamic
sphere at (0,1,5), `BODY|MotionType=Dynamic|Mass=1.000|GravityFactor=1.000`,
`SHAPE|ShapeType=Sphere|ForceUnique=True`. GravityFactor=1.000 ⇒ no
`PhysicsGravityFactor` baked ⇒ every training clip exercised the ADD path. Clips:
A_ZeroG (0–2s, scale=0, restore=true), B_ReverseG (1.5–3.5s, scale=-1, restore=true,
blendIn 0.5 — at t=1.75 weights 0.5/0.5 → `math.lerp(0,-1,0.5)=-0.5`),
C_PermanentMoonG (5–6s, scale=0.5, restore=false — permanence/poisoning exhibit; the
3.5–5.0s gap holds B's −1). Binding was the 14th table entry (B13 was lesson 15's
FilterOverrideTrack → the same GameObject); director restored afterward to
`Assets/Training/01-transform-position-track/PositionMastery.playable` (entry
survives the swap — keyed by track asset). The pre-lesson report printed only
`BINDINGS|count=13` (prior entries' contents never individually captured) — capture
full `PRE|binding|` lines yourself.
