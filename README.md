# BovineLabs Timeline Physics

Physics-focused Timeline tracks for DOTS projects built on BovineLabs Timeline Core and Unity Physics.
The package is a pure integration layer: it never reimplements the physics step.

## Architecture

*The philosophy, the flow, what's deliberately great, what's deliberately stupid, and the recipes.
Written 2026-07 after the Phase 2 consolidation (TrackBlendStateDriver, 153/153 tests).*

---

### 1. The philosophy in one sentence

> **A timeline clip is an opinion, not an action.**

Clips never touch a `PhysicsVelocity`. They *vote*. All votes for one body get blended into a single
per-body **desire** (`ActiveX.Config`), and a completely separate system — running on a different clock —
reads that desire and acts on the body. The whole package is this one sentence, stamped out ~16 times.

The reason is the **two-clock problem**:

| Clock | Group | Rate | Who lives here |
|---|---|---|---|
| Render | `TimelineComponentAnimationGroup` | variable, 30–240 Hz | Track systems (clip sampling, blending) |
| Fixed | `FixedStepSimulationSystemGroup` | 50 Hz, runs 0..N times per render frame | Apply systems, the physics solve |

If clips wrote forces directly, gameplay would depend on frame rate (this was *measured* on the old code:
a 1-second continuous force moved a body different distances at 30 vs 120 fps). So the architecture is a
**clock-domain crossing**, and every piece has a role on one side of the boundary:

```
  RENDER SIDE (per clip)                LATCH (per body)              FIXED SIDE (per body)
  "what do the clips want?"       "the agreed single desire"          "make the body do it"

  XAnimated ── blend ──────▶  ActiveX (enableable) + XState  ◀──── read by XApplySystem
  on clip entities              on the bound body entity            writes PhysicsVelocity /
                                                                    collider / gravity / ...
```

Hardware analogy: track systems are the front-end (sample, normalize, combine), `ActiveX` is a
double-buffered mailbox register, apply systems are the back-end scanning it out at fixed rate.

### 2. The cast, per track family

Every family (Force, Drag, GravityOverride, ShapeSwap, …) is the same five types:

| Type | Lives on | Job |
|---|---|---|
| `XData` | payload | Pure unmanaged config — what one clip means. |
| `XAnimated : IAnimatedComponent<XData>, IPreparable` | **clip** entity | `AuthoredData` (baked, immutable) + `Value` (this frame's animated copy). |
| `ActiveX : IActive<XData>` (enableable) | **body** entity | The latch. Enabled = "some clip wants this now"; `Config` = the blended desire. |
| `XState` | **body** entity | Per-activation memory: `Fired` latch, captured originals, `ElapsedTime`. |
| `XMixer : IMixer<XData>` | value | How overlapping clips combine (`Lerp` = crossfade, `Add` = stack). |

Since Phase 2, the render side of every family is one field in a ~40-line system shell:

```csharp
private TrackBlendStateDriver<XData, XAnimated, ActiveX, XMixer, XState> _driver;
// OnCreate: _driver.OnCreate(ref state, RearmPolicy.EveryActivation, new XState { Fired = false });
// OnUpdate: _driver.OnUpdate(ref state, ecb);
```

### 3. The flow

### 3.1 Render frame — the track driver pipeline (identical for all 16 families)

```
TimelineComponentAnimationGroup                       (render rate, after EntityLinkTargetPatch)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│   CLIP ENTITIES (one per timeline clip)                       BODY ENTITY (TrackBinding)  │
│                                                                                           │
│   1. RESET     clip just became active                                                    │
│                (ClipActive && !ClipActivePrevious)   ───────▶  XState = ResetValue        │
│                gated by RearmPolicy (see §4)                                              │
│                                                                                           │
│   2. PREPARE   Value = AuthoredData                                                       │
│                (undo whatever last frame's parameter                                      │
│                 animation left in Value)                                                  │
│                                                                                           │
│   3. BLEND     TrackBlendImpl: clip weights × Value  ───────▶  blendData { body → mix }   │
│                (core package; per-body hash map)                                          │
│                                                                                           │
│   4. DISABLE   body bound by an ENDING clip and                                           │
│                absent from blendData                 ───────▶  ActiveX.enabled = false    │
│                                                                (direct write, immediate)  │
│                                                                                           │
│   5. WRITE     per blendData entry                   ──ECB──▶  ActiveX.Config = Mixer(mix)│
│                (BeginSimulation ECB                            ActiveX.enabled = true     │
│                 ⇒ lands NEXT frame, see §6.1)                                             │
│                                                                                           │
│   6. HOOK      Force & Velocity only                 ───────▶  XState.ElapsedTime += dt   │
│                (per-body, via blendData — overlapping                                     │
│                 clips count once)                                                         │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Fixed step — the physics sandwich

```
FixedStepSimulationSystemGroup                                  (50 Hz, 0..N× per render frame)
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ PhysicsProducerGroup                    (everything the solver should integrate)          │
│   PhysicsClipGateSystem      crossing-aware first/last-frame flags for the trigger family │
│   KinematicsApply            ActiveForce/ActiveVelocity ──▶ PendingForce/PendingVelocity  │
│   PidApply / SplineFollow    controllers                 ──▶ PendingForce                 │
│   TriggerQuery/Force/…       contact events              ──▶ conditions, Pending*, spawns │
│   ProducerAccumulator        drain Pending* buffers      ──▶ PhysicsVelocity  (+resets)   │
│   ExternalCompose            drain PendingExternalForce  ──▶ ExternalVelocity;            │
│                              PhysicsVelocity += ExternalVelocity                          │
│ ══════════════════════════ Unity Physics solve ═══════════════════════════════════════   │
│ PhysicsModifierGroup                    (last word over what the solver produced)         │
│   ExternalDecompose          PhysicsVelocity -= ExternalVelocity; External *= e^(-k·dt)   │
│   DragApply                  exponential decay of INTENT velocity only                    │
│   VelocityOverride           ActiveVelocity Set modes stomp velocity                      │
│   Filter/ShapeSwap/Resize/   capture-restore mutations of collider & body params          │
│   Gravity/KinematicOverride                                                               │
│   VelocityClampApply         the final speed limit (external channel exempt, by design)   │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

The **producer / modifier split** answers one question: *does this effect need to see the solve result?*
Forces you want integrated → producer. Corrections/overrides of the integrated result → modifier.

### 3.3 The apply-side state machine (capture-restore families)

Gravity, Kinematic, FilterOverride, ShapeResize, ShapeSwap, VelocityClamp all run this per body:

```
                       ActiveX.enabled?
                     ┌───────┴────────┐
                  enabled          disabled
                     │                │
              state.Fired?      state.Fired?
              ┌──────┴─────┐    ┌─────┴─────┐
             no            yes  yes         no
              │             │    │           │
            ENTER         STAY  EXIT      (idle)
            capture       re-   restore
            original      apply original,
            apply         every Fired=false
            override      step
            Fired=true
```

**Why STAY re-applies every step:** several systems mutate the *same* target (a shape swap replaces the
collider blob a filter override wrote into) — re-asserting each step self-heals those fights, and makes
blended `Config` changes live mid-clip.

### 3.4 The determinism bridge (ElapsedTime / AppliedTime)

```
render frames:  |──16ms──|──16ms──|──16ms──|──16ms──|     track: ElapsedTime += dt while blended
fixed steps:       |─────20ms─────|─────20ms─────|        apply: consume (ElapsedTime − AppliedTime)
                                                                  AppliedTime = ElapsedTime
```

Continuous force/velocity integrates against the **delta of clip-active time**, not the fixed dt.
Total impulse = force × active-duration, independent of how render frames straddle fixed steps.
Any state that needs this implements `IElapsedTimeState` and gets the shared `AdvanceElapsedTimeJob`.

The clip-end tail (the last `ElapsedTime − AppliedTime` remainder, which a fixed-step-less clip-end used to discard)
is drained by the **fixed-step latch linger** (§6.1): the stale-disable keeps an undrained latch enabled until one
fixed tick consumes the frozen remainder. So the **total** is exact (`force × clip-active-duration`, never truncated).
What stays phase-dependent is only the **distribution** of that impulse across fixed steps — where the clip edges fall
inside a fixed step varies by up to one step (§6.2), so the intermediate velocity/position trajectory carries bounded
(≤ ~1 fixed-step) phase dependence. Honest claim: the running total is deterministic; the per-step placement is not.

### 3.5 The two motion channels

```
  PendingForce / PendingVelocity ───▶ INTENT   (lives in PhysicsVelocity)
      your own drag, clamps, and resets shape it — locomotion, dashes, thrust

  PendingExternalForce ───▶ ExternalVelocity   (separate standing component)
      knockback that SURVIVES your own braking: composed onto velocity before the
      solve, decomposed after, decays on its own clock (external-velocity.decay-rate)
```

Exactly two channels, deliberately: granularity was analyzed and stops paying past two.
`VelocityResetFlags.External` is the only thing allowed to parry the external channel.

### 4. RearmPolicy — the one enum that encodes the lifecycle philosophy

The old code had two reset jobs and each hand-rolled system silently copied one of them. Which one you
copied *was* the design decision, invisible in review. It's now an explicit parameter:

| Policy | Families | Why |
|---|---|---|
| `EveryActivation` | fire-once: Force, Velocity, Teleport, Ricochet, PIDs, Socket, Spline, Clamp | Each clip is one shot. Adjacent impulse clips must EACH fire, so the `Fired` latch re-arms on every clip start — even mid-span. |
| `SpanStart` | capture-restore: Gravity, Kinematic, Filter, ShapeResize, ShapeSwap | The captured "original" must survive touching clips. If clip B re-armed mid-span it would re-capture clip A's *override* as the original, and the final restore would leak it. Reset only when `ActiveX` is genuinely disabled (a real gap). |

Pick wrong and you get either double-fires or the override-leak — `PhysicsTrackLifecycleTests` pins both.

### 5. Advantages (why this architecture earns its complexity)

1. **Framerate-independent gameplay.** The clock-domain split + elapsed-time bridge is the whole point.
   Same timeline, same result at 30 and 240 fps.
2. **Per-body convergence.** N overlapping clips → exactly ONE `ActiveX` per body per family. Apply
   systems know nothing about clips, tracks, timelines, or blending. That boundary is *hard*.
3. **The latch is an open seam.** Anything may drive `ActiveX` — AI, network code, a debug console, a
   test. Most unit tests do exactly this: poke `ActiveX` + run the apply system, no timeline involved.
4. **The Pending buffer contract is an open seam too.** Game code appends `PendingForce` /
   `PendingExternalForce` from any producer-group system without touching the package —
   `Sample~/Knockback Ring/DirectionalKnockbackSystem` is the 40-line proof.
5. **Runtime is structurally quiet.** Activation toggles enable bits (chunk-cheap); the only structural
   changes are capture-restore ECB adds/removes. Everything is Burst; the blend map is the one sync point.
6. **A new track is now a stamp.** One data file (five types), a ~40-line shell, one baker line, one
   registration block. The lifecycle (reset semantics, stale-disable, blend, write) cannot be gotten
   wrong because it isn't written — it's parameterized.
7. **Testable halves.** Track lifecycle and apply behavior test independently; 153 tests do.

### 6. Stupidities & sharp edges (deliberate ceilings — know them before "fixing" them)

1. **One frame of activation latency; deactivation is now drain-aware.** WRITE goes through the BeginSimulation
   ECB (lands next frame); the effect starts one render frame late. The DISABLE *used* to be immediate, which meant
   a clip whose active window straddled no fixed tick inside that delayed enable window was dropped (impulse never
   fired, teleport never happened) and a continuous force lost its unconsumed tail. Fixed for the Force / Velocity /
   PID / Teleport families by the **fixed-step latch linger** (the promised "gate-style crossing detection, not
   make-the-ECB-immediate"): `DisableAbsentDrainableTrackJob` only disables a latch that is already
   `IDrainableLatchState.IsDrained`; otherwise it keeps it enabled and marks it `Orphaned`, and the fixed-step
   `PhysicsLatchDrainFinalizeSystem` (Modifier group, OrderLast) disables it only *after* an apply has serviced it.
   Every disable is therefore preceded, in the same fixed tick, by an apply that observed the latch → no
   `Active*`-driven effect is dropped, and the continuous tail is drained rather than truncated. A drained latch
   still disables immediately (zero deactivation latency for the common path). The trigger family keeps its own
   per-clip `PhysicsClipGate`; the capture-restore families (Gravity/Kinematic/Filter/Shape) deliberately keep the
   plain immediate disable because their disable *is* the OnExit-restore edge.
2. **Clip edges jitter by up to one fixed step.** Enabled toggles at render rate; fixed steps sample it.
   Where inside a fixed step a clip boundary lands varies run to run. The elapsed-time bridge makes the
   *total* deterministic; the *edge placement* is not. Accepted ceiling.
3. **`WriteActiveJob` writes `default(TActive)` + Config.** Any extra field someone adds to an `ActiveX`
   gets zeroed every frame. `ActiveX` is Config-only *by contract*, enforced by nothing but this line.
4. **Blend identity is structural (the `Present` hack).** Core's blend fills empty slots with
   `default(TData)`, so a mixer can't tell "empty slot" from "authored all-zero clip" (a legitimate
   hard-brake). Velocity and Filter carry a `byte Present = 1` set in their builders as a local fix —
   core's `MixData` is read-only PackageCache, so the wart lives here. Don't remove it; a zero-velocity
   stop clip silently losing crossfades was a real bug (B1).
5. **Generic jobs are an IL2CPP landmine.** Jobs scheduled from generic driver code have no IL-visible
   instantiation → no job-reflection data → player SIGSEGV (editor is fine — Mono JIT). Every closed
   instantiation is hand-registered in the runtime `AssemblyInfo.cs` (~7 lines per track, including core's
   `TrackBlendImpl` nested jobs). **Adding a track without its registration block ships a working editor
   and a crashing player.** This is the #1 trap for the next contributor.
6. **Six bespoke apply-side state machines.** The enter/stay/exit pattern in §3.3 is re-implemented per
   capture-restore family because their "lanes" genuinely differ (in-place blob mutation vs ECB
   add/remove vs velocity lanes vs the gravity↔kinematic cross-check). Unifying was evaluated and
   rejected — a generic effect interface needs a context mega-struct and relocates more than it deletes.
   Cost of the decision: the exit-restore-ordering bug class must be re-reviewed per system.
7. **Destroyed clips don't disable the latch.** `DisableAbsentTrackJob` runs on *deactivated* clip
   entities. If clip entities are destroyed outright (timeline despawn) nothing disables `ActiveX`; the
   last desire stays latched. In practice LifeCycle destroy flows deactivate first — but drive timelines
   from custom code and you own this.
8. **Mixer `Add` semantics are per-family folklore.** Sum (drag), dominant-by-strength (PID),
   enum-order priority (velocity), return-b (gravity/clamp). Overlapping clips do whatever the family's
   mixer decided years ago. Check the mixer before stacking clips.

### 7. Useful things (recipes)

**Knock a body around from game code** — append to the seam, never touch `PhysicsVelocity` directly:
```csharp
// any system in PhysicsProducerGroup, [UpdateBefore(typeof(PhysicsProducerForceAccumulatorSystem))]
pendingExternal.Add(new PendingExternalForce { Linear = away * strength });  // survives their brakes
pendingForce.Add(new PendingForce { Linear = thrust });                      // shaped by their brakes
```

**Drive a track effect without a timeline** (also the unit-test idiom):
```csharp
em.AddComponentData(body, new ActiveDrag { Config = new PhysicsDragData { Linear = 8f } });
// enableable: toggle with em.SetComponentEnabled<ActiveDrag>(body, true/false)
```

**See what's happening:** every family has a Quill gizmo behind a ConfigVar —
`physicsforce.draw-enabled`, `draggizmo.draw-enabled`, `triggerquerygizmo.draw-enabled`,
`externalvelocitygizmo.draw-enabled` (the red knockback arrow), `sweptgizmo.draw-enabled`, …

**Add a new track family — the checklist:**
1. `XData` + `XAnimated : IAnimatedComponent<XData>, IPreparable` + `ActiveX : IActive<XData>`
   (+ `XState` if it fires or captures) + `XMixer` — one file in the Data assembly.
2. Builder (`ApplyTo` adds the Animated) + Clip/Track authoring classes.
3. One `Ensure<XAnimated, ActiveX, XState>` line in `PhysicsTimelineBakingSystem`
   (+ `EnsureFlags.AccumulationBuffers` if it produces motion).
4. The ~40-line shell: `TrackBlendStateDriver<...>` + a `RearmPolicy` — decide fire-once vs
   capture-restore *first*, it is the design decision.
5. Apply system in Producer (solver should integrate it) or Modifier (corrects the solve result).
6. **The registration block in `AssemblyInfo.cs`** — all seven generic job instantiations. Skipping this
   compiles, passes tests, and crashes IL2CPP players.
7. If it needs elapsed-time determinism: `XState : IElapsedTimeState` + schedule
   `AdvanceElapsedTimeJob` over the returned blend map (copy the Force shell).

**Where does my new fixed-step system go?** Ask: *do I need the solve result?*
No → `PhysicsProducerGroup` (it gets integrated). Yes → `PhysicsModifierGroup` (last word).
Clamp-like systems go last in Modifier; remember the external channel bypasses clamps by design.

## Package name

`com.bovinelabs.timeline.physics`

## Tracks

| Track | Kind | What it does |
| --- | --- | --- |
| Physics Velocity | Producer/Modifier | Set (post-step) or add (pre-step) linear/angular velocity, instant or continuous, in a configurable space |
| Physics Force | Producer | Continuous force or one-shot impulse, fixed vector or toward/away from a target |
| Physics Drag | Modifier | Exponential linear/angular velocity decay |
| Physics Velocity Clamp | Modifier | Hard linear/angular speed limits (negative limit = unlimited) |
| Physics Gravity Override | Stateful | Override `PhysicsGravityFactor`, restoring the original on exit |
| Physics Kinematic Override | Stateful | Toggle `PhysicsMassOverride.IsKinematic`, optionally zeroing velocity/gravity |
| Physics Filter Override | Stateful | Override the collision filter on a **unique** collider, restoring on exit |
| Linear / Angular PID | Producer | Force/torque PID controllers tracking a target position/orientation |
| Physics Teleport | Stateful | Candidate-based teleport with clearance, line-of-sight, azimuth/elevation patches, and configurable reference frame |
| Physics Ricochet | Stateful | Raycast with grazing-angle ricochet and terminal-hit condition routing |
| Trigger Condition / Force / Instantiate | Event | React to stateful trigger/collision events: route conditions, apply falloff forces, spawn prefabs |
| Chain Follow / Grab, Socket Return | Producer | Spring-driven chain weapons, grab joints, and socket recall |

## Authoring requirements

- Bodies driven by force/velocity tracks receive `PendingForce`/`PendingVelocity` buffers and `Active*`/state components automatically via `PhysicsTimelineBakingSystem`.
- The Filter Override track requires the bound body's collider to be **Force Unique**; shared collider blobs are skipped with a warning, because mutating a shared blob would rewrite the filter on every body sharing it.

## Dependencies

See `package.json` for exact versions: `com.bovinelabs.core`, `com.bovinelabs.timeline(.core)`, `com.bovinelabs.timeline.entitylinks`, `com.bovinelabs.reaction`, `com.unity.entities`, `com.unity.physics`, `com.unity.timeline`.

## Sample

The package includes a sample at `Sample~/Physics`. Import it through the Unity Package Manager Samples UI.

## Testing

Edit-mode tests live in `BovineLabs.Timeline.Physics.Tests` and can be run headlessly; see `AGENTS.md`.
