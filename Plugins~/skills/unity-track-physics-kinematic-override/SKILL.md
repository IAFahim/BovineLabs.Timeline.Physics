---
name: unity-track-physics-kinematic-override
description: Master of PhysicsKinematicOverrideTrack + PhysicsKinematicOverrideClip (package BovineLabs.Timeline.Physics) — while-timeline-active kinematic freezing via PhysicsMassOverride, unconditional config-blind exit restore, the gravity cross-track conflict matrix. Portable to any project containing the package; worked example from vex-ee. Use when a designer asks "freeze this body / carry it on rails during the clip".
---

# PhysicsKinematicOverrideTrack specialist

## 1. SCOPE

You are the specialist for **`PhysicsKinematicOverrideTrack`** and
**`PhysicsKinematicOverrideClip`** from the package `BovineLabs.Timeline.Physics`
(namespace `BovineLabs.Timeline.Physics.Authoring.Kinematics`). Scope: exactly
this track family — authoring the track/clips in a `.playable` TimelineAsset,
wiring a SubScene PlayableDirector (GameObject binding), and the runtime
kinematic-override semantics including the gravity conflict matrix. Physics
bodies/stages belong to a stage/physics-setup specialist — a missing physics
body is a missing-prerequisite report, never something you create. Family
patterns live in `unity-track-physics-filter-override` (its **PHYSICS FAMILY
SHARED PATTERNS** section); gravity-component mechanics in
`unity-track-physics-gravity-override` (add/mutate/remove paths, capture
poisoning, EndFixedStep ECB latency). Cite them; don't re-derive.

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`;
use the editor per `unity-cli`. That shared skill owns the discovery preamble
(§1), the SubScene bracket (§2), the undo-appendix structure (§3), and the
fresh-load verification protocol (§4) — this skill keeps ONLY the
kinematic-override-unique facts below. This track binds a **`GameObject`**
(`[TrackBindingType(typeof(GameObject))]`), so the bracket's bind step passes the
GameObject ITSELF, not a component (`SetGenericBinding(track, bodyGo)`).

## 2. TYPE FACTS

| Type | Facts |
|---|---|
| `PhysicsKinematicOverrideTrack` | `BovineLabs.Timeline.Physics.Authoring.Kinematics`, asm `BovineLabs.Timeline.Physics.Authoring`, sealed, EMPTY body, base `DOTSTrack`. `[TrackClipType(typeof(PhysicsKinematicOverrideClip))]`, `[TrackBindingType(typeof(GameObject))]`, `[TrackColor(0.5,0.5,0.5)]`, `[DisplayName("BovineLabs/Physics/Kinematic Override")]`. |
| `PhysicsKinematicOverrideClip` | sealed, base `DOTSClip`, `ITimelineClipAsset`, **`clipCaps => ClipCaps.None`** (first-writer overlap races, like Filter; `DiscreteMixer` is dead code), `duration => 1` (seed only). |
| `PhysicsKinematicOverrideData` | `BovineLabs.Timeline.Physics`, asm `...Physics.Data`: `bool IsKinematic; bool ZeroVelocityOnEnter; bool ZeroGravity;` — **NO RestoreOnExit field.** |
| `PhysicsKinematicOverrideAnimated` | `IAnimatedComponent<PhysicsKinematicOverrideData>` — `AuthoredData` + `Value`, on the CLIP entity. |
| `ActiveKinematicOverride` | IComponentData + **IEnableableComponent**: `PhysicsKinematicOverrideData Config` — on the BINDING entity, added DISABLED at bake. |
| `PhysicsKinematicOverrideState` | `bool Fired; float OriginalGravityScale; bool AddedGravityComponent; bool AddedMassOverrideComponent; byte OriginalIsKinematic;` — binding entity. Source comment: `// PhysicsMassOverride uses byte for IsKinematic`. |
| `Unity.Physics.PhysicsMassOverride` | `byte IsKinematic; byte SetVelocityToZero;` (quoted from `PhysicsComponents.cs`). |
| Systems | `PhysicsKinematicOverrideTrackSystem` (`TimelineComponentAnimationGroup`, `[UpdateAfter(EntityLinkTargetPatchSystem)]`, per rendered frame); `PhysicsKinematicOverrideApplySystem` (**`PhysicsModifierGroup`** = FixedStep AFTER `PhysicsSystemGroup`; query `WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)`). |

### Field table (camelCase YAML, fresh-instance reflection)

| Field | Type | Default | Meaning |
|---|---|---|---|
| `isKinematic` | bool | `True` | `PhysicsMassOverride.IsKinematic=1` while active — infinite-mass kinematic, **still velocity-driven**. |
| `zeroVelocityOnEnter` | bool | `True` | Direct `PhysicsVelocity` Linear/Angular = 0 on the ENTER edge only (NOT the `SetVelocityToZero` byte — that stays untouched). |
| `zeroGravity` | bool | `True` | `PhysicsGravityFactor.Value = 0` while active — DEFERRED entirely if a GravityOverride regime is active. |

`DEFAULTS|isKinematic=True|zeroVelocityOnEnter=True|zeroGravity=True|duration=1|clipCaps=None` (quoted).
YAML (raw-read verified): bools as 1/0; no blend/ease YAML — ClipCaps.None.
Bake (quoted from `PhysicsKinematicOverrideClip.Bake`): unconditional — no guard,
no LogError; `PhysicsKinematicOverrideBuilder.ApplyTo` adds
`PhysicsKinematicOverrideAnimated{AuthoredData}`. Binding-entity pair from the
central `PhysicsTimelineBakingSystem` (identical family shape; `Entity.Null`
continue ⇒ unbound track = total silent no-op).

Distinctions from the Filter/Gravity siblings:
1. **PhysicsModifierGroup** — the apply system runs AFTER `PhysicsSystemGroup`
   (Gravity's runs before, in the producer group); even in-place enter writes
   first bind the *next* physics step.
2. **NO restoreOnExit field** — exit ALWAYS restores; kinematic overrides cannot
   be made permanent via the timeline. Worse: `OnExit` never receives the Config
   — the exit gravity restore is **config-blind** (fires even for
   `zeroGravity=false` regimes).
3. **Touches TWO components** — `PhysicsMassOverride` (owned) AND
   `PhysicsGravityFactor` (shared with the Gravity track, defended by the
   defer-to-gravity guard) — hence THE CONFLICT MATRIX below.

### Runtime semantics (source-quoted)

The track system runs the family kernel per rendered frame (filter skill,
patterns 4–5): `ResetStateTrackJob` reseeds the State (`Fired=false,
OriginalGravityScale=1f, AddedGravityComponent=false,
AddedMassOverrideComponent=false, OriginalIsKinematic=0`) on clip-activation
edges only while `ActiveKinematicOverride` is disabled; no `ClipWeight` is ever
baked (ClipCaps.None ⇒ first-writer-wins); `DisableStaleTrackJob`
direct-write-disables (`ActiveLookup.SetComponentEnabled(target, false)` — no
ECB) only on the timeline-deactivation edge.
`PhysicsKinematicOverrideApplySystem` (`PhysicsModifierGroup`, after the step)
runs the Fired machine per fixed step: **enter** — optional one-shot
`PhysicsVelocity` wipe; `PhysicsMassOverride` mutate-or-ECB-add (capturing
`OriginalIsKinematic`/`AddedMassOverrideComponent`); gravity capture-and-zero
only `if (config.ZeroGravity && !hasActiveGravityOverride)` (mutate-or-add,
mirroring the gravity skill); **stay** — re-assert `IsKinematic` and (same guard,
NO re-add) gravity 0 every step; **exit** (timeline deactivation, no restore flag
exists) — unconditional three-branch restore of the mass piece (remove-if-added /
restore-in-place / re-add-original) plus, *only if no GravityOverride regime is
active and regardless of `config.ZeroGravity`*, the same three-branch restore of
`PhysicsGravityFactor` from the State. All adds/removes ride the EndFixedStep ECB
(one-fixed-step add-path latency; set-on-duplicate adds).

**Kinematic = velocity-driven (the Velocity-track bridge):** quoted from
`PhysicsWorldBuilder.cs` — a body with `MassOverride.IsKinematic != 0` gets
`defaultPhysicsMass` (infinite mass, force/impulse/gravity-immune, unstoppable in
collision response) but its `PhysicsVelocity` is still integrated every step. The
kinematic window is the natural substrate for deterministic carries: a
Velocity-track clip layered in the same window can write `PhysicsVelocity`
directly, and the kinematic exit hands whatever velocity remains straight back to
dynamic simulation.

### THE CONFLICT MATRIX (gravity × kinematic on one body — the headline)

`zeroGravity` writes the SAME component (`PhysicsGravityFactor`) that
`PhysicsGravityOverrideTrack` owns. The defense is the **defer-to-gravity guard**,
computed per entity per fixed step from the **live ENABLED bit** of
`ActiveGravityOverride` — the very bit that admits the gravity track's own apply
system (quoted):

```csharp
var hasActiveGravityOverride = hasGravityOverride &&
                               chunk.IsComponentEnabled(ref ActiveGravityOverrideHandle, i);
// ENTER:  if (config.ZeroGravity && !hasActiveGravityOverride) { ... }
// STAY:   if (config.ZeroGravity && lanes.HasGravityFactor && !hasActiveGravityOverride) { ... }
// EXIT:   if (!hasActiveGravityOverride) { ... }   // the ENTIRE gravity restore — config-blind
```

**Safe by design (scenarios 1–3):** (1) **Overlap** — gravity regime already
active when kinematic enters: enter/stay gravity blocks guard-skipped; gravity
owns the component end to end (MassOverride + velocity-zero still apply).
(2) **Sequential** — each regime captures and restores in turn; no shared window.
(3) **Same-frame enters** — both Active bits flip in the same BeginSimulation
playback; gravity's apply (producer, before the step) enters first; kinematic's
apply (modifier, after) reads the same enabled bit → defers. No interleaving
exists where gravity acts and kinematic doesn't see it.

**Residual hazards (where the guard cannot save you):**

- **4a — kinematic-first cross-timeline poisoning.** Kinematic timeline K
  (zeroGravity=true, no gravity active) ADDs `PhysicsGravityFactor{0}`. Gravity
  timeline G then starts: mutate path captures **0 as original** (poisoned). K
  ends while G is active: kinematic's exit guard is TRUE → gravity restore
  skipped — **the `{0}`-add is orphaned** (State reseeded next run; the knowledge
  that kinematic added it is gone). G ends with restoreOnExit=true → "restores" 0.
  **Permanent `PhysicsGravityFactor{0}` no exit will ever remove — the body falls
  weightless forever.**
- **4b — same-timeline shared-end clobber.** Both tracks' DisableStale jobs fire
  on the same `TimelineActivePrevious && !TimelineActive` edge, dropping BOTH
  Active bits the same frame, before the apply systems' exits. Kinematic's exit
  then sees the guard false and runs its gravity restore even though gravity's
  exit ran the same fixed step — producer-before-modifier ordering makes
  **kinematic the last writer**. Mutate-path bodies (baked factor, e.g. 0.5):
  gravity restores 0.5 → kinematic overwrites with its State (the seeded 1.0 if
  its enter deferred). Gravity's `restoreOnExit=false` permanence intent is
  equally defeated. ADD-path bodies are accidentally benign: gravity's queued
  RemoveComponent plays back after kinematic's in-place write and wins.
- **4c — the `zeroGravity=false` exit anomaly.** `OnExit` ignores the config: a
  regime whose entering clip had `zeroGravity=false` STILL runs the exit gravity
  branch with the seeded State — on a component-less body it ADDs
  `PhysicsGravityFactor{1f}` (behaviorally neutral, permanent archetype change);
  on a baked factor ≠ 1 it **overwrites the baked factor with 1.0**. A "pure
  rails" timeline on a moon-gravity body silently normalizes it to Earth gravity
  at timeline end.
- **4d — double-add: adjudicated UNREACHABLE.** Both systems ECB-adding in one
  fixed step requires gravity's Active bit enabled AND read as disabled by
  kinematic — a contradiction; and ECB `AddComponent` is set-on-duplicate anyway.
  The real first-tick artifact is the family add-path latency (EndFixedStep ECB
  plays back after that tick's physics step).

**THE DESIGNER RULE (supersedes the curriculum's softer rule):** **never put a
PhysicsKinematicOverrideTrack and a PhysicsGravityOverrideTrack on the same body
in the same timeline** — the guard protects the active window, but the shared end
makes kinematic's config-blind exit the last writer (4b), and cross-timeline
mixes orphan kinematic's added component into a poisoned gravity capture (4a).
"Prefer `zeroGravity=false` + an explicit gravity clip" is only sufficient on
add-path bodies (authored factor = 1); on a baked factor ≠ 1 even
`zeroGravity=false` exits clobber it to the seeded 1 (4c). Hard-safe: disjoint
timelines that never overlap and never end while the other runs, or give gravity
duty to exactly one track. `isKinematic=true` already makes gravity moot while
active — `zeroGravity=true` is only load-bearing when `isKinematic=false`.

**Conflict pre-flight (MANDATORY before authoring):** during discovery (per
`unity-timeline-track-authoring` §1), scan every timeline asset reachable from
directors that bind this body for a `PhysicsGravityOverrideTrack`. If one shares
the body, STOP and surface the conflict matrix (4a/4b/4c) to the designer before
authoring anything.

### Traps & DO/DON'T (each source-proven, vex-ee 2026-06)

- **DO rely on exit ALWAYS restoring — no permanence exists** — reflection shows
  exactly three clip fields and `OnExit` takes no config; the family's only track
  (verified through topic 21) with mandatory symmetric restore. If a body must
  STAY kinematic after the timeline, this track CANNOT do it (report out-of-domain).
- **DON'T expect `zeroVelocityOnEnter` per clip — it fires once per TIMELINE
  activation** — the wipe lives only in `OnEnter`, and `ResetStateTrackJob`
  reseeds only while `ActiveKinematicOverride` is disabled (which only
  `DisableStaleTrackJob` does, on the timeline-deactivation edge); a later clip in
  the same run does NOT re-zero (Fired still true), and scrubbing back into an
  earlier clip only re-runs stay.
- **DO trust the three-branch exit for exact archetype recovery** —
  remove-if-added / restore-in-place / re-add-original per piece (same shape as
  the gravity skill's exit); a body that baked neither component takes ADD on
  enter, REMOVE on exit. Footnote: the mutate paths write ONLY `IsKinematic`,
  preserving an external `SetVelocityToZero` byte — but the re-add-if-vanished
  branch reconstructs `new PhysicsMassOverride{IsKinematic = original}`, silently
  resetting `SetVelocityToZero` to 0.
- **DON'T self-heal-assume the gravity piece in stay** — kinematic's stay has NO
  gravity re-add (contrast Gravity's stay, which ECB-re-adds); an external removal
  mid-regime stays removed until exit's third branch.
- **DO know gravity is moot under `isKinematic=true`** — quoted
  `PhysicsWorldBuilder.cs`: kinematic bodies get infinite `defaultPhysicsMass` and
  gravity is skipped regardless of `PhysicsGravityFactor`.
- **DON'T overlap kinematic clips** — `ClipCaps.None` ⇒ no `ClipWeight` ⇒
  first-writer-wins races (the `DiscreteMixer` is dead code, exactly like
  Filter's). Stagger them.
- **Regime rule (family pattern 5)** — with multiple kinematic clips, dynamic
  behavior returns only at TIMELINE end: gaps hold the last Config (stay keeps
  re-asserting `IsKinematic`); one enter/exit pair brackets the whole run.
- **DO note the silence profile (family rule 7, fully silent variant)** — bake
  unconditional, unbound track = `Entity.Null` continue, runtime has NO loud
  failure at all (no blob ⇒ not even Filter's shared-blob warning). A clean
  console proves nothing.

## 3. CLIP PATTERNS (the bracket's track-specific middle)

Fills the `unity-timeline-track-authoring` §2 bracket's clip middle; everything
else (open/wire/save/restore) is that skill's. NEVER overlap kinematic clips
(first-writer-wins race) — stagger them. The bind step passes the
**GameObject itself** (`[TrackBindingType(typeof(GameObject))]`), not a component.

```csharp
var track = timeline.CreateTrack<BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideTrack>(null, trackName);

// Pattern FREEZE-FRAME (the all-defaults clip): "freeze this body in place during
// the clip" — immovable infinite-mass kinematic, velocity wiped once on enter,
// gravity zeroed; still PUSHES dynamic bodies it touches. Exit restores everything.
var clipA = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideClip>();
clipA.start = 0; clipA.duration = 2; clipA.displayName = "<clipName>";
var a = (BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideClip)clipA.asset;
a.isKinematic = true; a.zeroVelocityOnEnter = true; a.zeroGravity = true;

// Pattern RAILS / CARRY MOMENTUM: "carry this body on rails keeping its speed" —
// keeps its PhysicsVelocity and glides, immune to gravity/forces — drivable by a
// Velocity/PID track in the same window; exit hands remaining velocity back to
// dynamic simulation.
var clipB = track.CreateClip<BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideClip>();
clipB.start = 3; clipB.duration = 2; clipB.displayName = "<clipName>";
var b = (BovineLabs.Timeline.Physics.Authoring.Kinematics.PhysicsKinematicOverrideClip)clipB.asset;
b.isKinematic = true; b.zeroVelocityOnEnter = false; b.zeroGravity = false;
```

**Path prediction (record in your card during §1 discovery of the body):** Dynamic
⇒ no `PhysicsMassOverride` baked ⇒ the mass piece takes ADD on enter / REMOVE on
exit. Gravity piece: authored GravityFactor=1 + Dynamic ⇒ ADD path; ≠1 or
Kinematic (always baked factor 0) ⇒ MUTATE path (gravity skill's conditional-bake
quote). ForceUnique is irrelevant here (no blob is touched).

## 4. WORKED EXAMPLE DELTA (vex-ee) — example only; rediscover, never assume

Shared stage is `unity-timeline-track-authoring` §5. This track's delta:
- Body: **Stage_PhysicsBall** — dynamic sphere at (0,1,5), fresh-load dump
  `BODY|MotionType=Dynamic|Mass=1.000|GravityFactor=1.000`,
  `SHAPE|ShapeType=Sphere|ForceUnique=True`. GravityFactor=1.000 ⇒ no
  `PhysicsGravityFactor` baked ⇒ gravity piece ADD path; Dynamic ⇒ no
  `PhysicsMassOverride` baked ⇒ mass piece ADD on enter / REMOVE on exit.
- Asset: `Assets/Training/17-physics-kinematic-override-track/KinematicMastery.playable`
  — one track `KinematicTrack`, clips A_FullFreeze (0–2s, isKinematic/zeroVelocity/
  zeroGravity all true — the freeze-frame exhibit; B's t=3 activation does NOT
  re-zero velocity, Fired still true) and B_RailsCarryMomentum (3–5s, isKinematic
  true, zeroVelocityOnEnter/zeroGravity false — the momentum carry exhibit).
- Wiring: binding `KinematicTrack → Stage_PhysicsBall (GameObject)`, the 15th table
  entry (B13 = lesson-15 FilterOverrideTrack, B14 = lesson-16 GravityTrack, same
  GameObject but on swapped-out assets ⇒ no LIVE conflict). Director restored after
  to `Assets/Training/01-transform-position-track/PositionMastery.playable` (entry
  survives the swap — keyed by track asset). The vex-ee report captured only
  `BINDINGS|count=14` pre-lesson with B13/B14 identified — the other 12 entries'
  contents were never individually captured; capture full `PRE|binding|` lines
  yourself per the shared §1.
