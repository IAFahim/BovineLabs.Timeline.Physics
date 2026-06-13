---
name: unity-track-physics-filter-override
description: Master of PhysicsFilterOverrideTrack + PhysicsFilterOverrideClip (package BovineLabs.Timeline.Physics) — while-timeline-active collision-filter blob mutation, the ForceUnique requirement, the timeline-end-not-clip-end restore; carries the PHYSICS FAMILY SHARED PATTERNS reference. Portable to any project containing the package; worked example from vex-ee.
---

# PhysicsFilterOverrideTrack specialist

## 1. SCOPE

You are the specialist for **`PhysicsFilterOverrideTrack`** and
**`PhysicsFilterOverrideClip`** from the package `BovineLabs.Timeline.Physics`
(namespace `BovineLabs.Timeline.Physics.Authoring.Filters`). Scope: exactly this
track family — the track/clips, the GameObject binding, the ForceUnique
prerequisite, and the runtime filter-override semantics. Physics bodies/stages
belong to a stage/physics-setup specialist — a missing physics body is a
missing-prerequisite report, never something you create. Gravity →
`unity-track-physics-gravity-override`; kinematic freezing →
`unity-track-physics-kinematic-override`. **This skill also carries the PHYSICS
FAMILY SHARED PATTERNS reference** (§2) that sibling physics skills cite instead
of re-deriving.

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`;
use the editor per `unity-cli`. That shared ceremony owns discovery (its §1 — the
package-exists check resolves `BovineLabs.Timeline.Physics.Authoring.Filters.PhysicsFilterOverrideTrack`
in `BovineLabs.Timeline.Physics.Authoring`; the bind target is a **GameObject**,
discovered via `Unity.Physics.Authoring.PhysicsBodyAuthoring` by COMPONENT not
name), the SubScene bracket (its §2), the undo appendix structure (its §3), and
the fresh-load verification protocol (its §4). The track-specific deltas to those
sections are in §3–§4 below.

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
(Concrete walk in §4.)

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

## 3. TRACK-SPECIFIC DELTAS TO THE CEREMONY

### Discovery (unity-timeline-track-authoring §1) — track prerequisite

Discover the physics body by COMPONENT (`PhysicsBodyAuthoring`), never by name,
per the shared §1 D4. ZERO bodies → missing prerequisite ("no
PhysicsBodyAuthoring in the SubScene — a physics-stage specialist must add one;
I override filters, I don't create bodies"). **This track's readiness test:
`ForceUnique` on the chosen body's shape must be true**, else the override
warn-and-skips at runtime (silently in Bursted players). Read it from the
`PhysicsShapeAuthoring` (`ForceUnique` property, or SerializedObject
`m_ForceUnique`). If false: report as a Gap, or — only with designer approval (it
changes the body's bake) — flip it (see §3 conditional recipe), capturing
`PRE|ForceUnique=<bool>` and journaling it. Capture `PRE|ForceUnique` only if you
will flip it.

### Bracket middle (unity-timeline-track-authoring §2) — the clip patterns

`CreateTrack<...Filters.PhysicsFilterOverrideTrack>(null, trackName)`; bind the
**GameObject itself** (`director.SetGenericBinding(track, bodyGo)`), not a
component. Clip fields set on `(PhysicsFilterOverrideClip)clip.asset`. Masks are
raw uints; both masks always write. The 3 patterns (designer intent → wiring):

- **GHOST — phase through everything, restore at timeline end.**
  `belongsToOverride = 0; collidesWithOverride = 0; restoreOnExit = true;`
- **SELECTIVE — only collide with specific layer(s).**
  `collidesWithOverride = 1u<<N` (raw uint mask); leave `belongsToOverride =
  0xFFFFFFFF` only if "everything" is acceptable for what OTHERS see of this
  body. NEVER overlap filter clips (first-writer-wins race) — stagger them.
- **PERMANENT — leave the override in place.** `restoreOnExit = false` on the
  LAST clip. WARN the designer: it also poisons every later capture on this
  collider (capture-poisoning trap, §2).

Clip starts/durations/masks are example choices, not package constants.

**Conditional recipe — enable ForceUnique** (only when discovery found it false
AND designer approved): inside the SubScene bracket, print + journal
`PRE|ForceUnique=<shape.ForceUnique>`; then `shape.ForceUnique = true;` (or
SerializedObject `m_ForceUnique` + ApplyModifiedProperties); `SetDirty(shape)`;
`SaveScene(subScene)`. Fresh-load verify.

### Undo appendix (unity-timeline-track-authoring §3) — extra artifact

Beyond the standard director-restore + asset-delete, add UNDO-3 ONLY if the
ForceUnique flip ran: SubScene bracket; find the shape by captured hierarchy
path; `shape.ForceUnique = <CAPTURED PRE value>`; SetDirty; SaveScene; restore
parent. The runtime effect (blob mask mutation, regime state, capture poisoning)
exists ONLY in play mode and is never serialized — the journal covers only
authoring artifacts; the poisoning hazard is a designer warning under Gaps, not a
journal entry.

### Verification (unity-timeline-track-authoring §4) — track read-backs

Asset dump fields: `belongsToOverride` / `collidesWithOverride` / `restoreOnExit`
(all clips `caps=None`). Raw YAML: uints as decimal, bools as 1/0; NO blend/ease
YAML. Prerequisite re-check: bound body MotionType/Mass/ShapeType and
**`m_ForceUnique=True`** (False ⇒ warn-and-skip at runtime; silent in Bursted
players). Binding: `BINDING|<trackName>|bound=<bodyGoName> (UnityEngine.GameObject)`.
This pipeline is bake-silent even when misconfigured — silence is expected, not
evidence (family pattern 7).

## 4. WORKED EXAMPLE DELTA (vex-ee training stage) — rediscover, never assume

Beyond the shared stage (unity-timeline-track-authoring §5): bind target is
**Stage_PhysicsBall** — dynamic sphere at (0,1,5), `PhysicsBodyAuthoring`
(Dynamic, Mass=1) + `PhysicsShapeAuthoring` (sphere r=0.5) + LifeCycle + Targets
(Target=Stage_Target). **ForceUnique history:** lesson-15 dump read
`m_ForceUnique=False` (would warn-and-skip); corrected to `True` AFTER lesson 15
by the stage owner, OUTSIDE the trainee's journaled run — re-verify live (so no
PRE capture of the flip exists in the report; lesson-time value was False).
Stage_TriggerZone also exists; NOT this track's binding target.

Asset: `Assets/Training/15-physics-filter-override-track/FilterOverrideMastery.playable`
— one track `FilterOverrideTrack`, clips A_Ghost (0–2s, belongsTo=0,
collidesWith=0, restoreOnExit=true), B_OnlyLayer1 (3–5s, collidesWith=2,
belongsTo left 0xFFFFFFFF, restoreOnExit=true), C_PermanentGhost (6–7s, both 0,
restoreOnExit=false — the permanence/poisoning exhibit). Regime walk: ghost at
t=0, STAYS ghost through the 2–3s gap, B's masks 3–6s, ghost again 6–7s; at
timeline end the exit runs once with C's `RestoreOnExit=false` → permanently
ghost. Binding = `FilterOverrideTrack → Stage_PhysicsBall (UnityEngine.GameObject)`,
the 13th table entry; director restored afterward to
`Assets/Training/01-transform-position-track/PositionMastery.playable` (entry
SURVIVES the swap — keyed by track asset). The pre-lesson report printed only
`BINDINGS|count=12` (12 prior entries' contents were never individually captured;
capture full `PRE|binding|` lines yourself).
