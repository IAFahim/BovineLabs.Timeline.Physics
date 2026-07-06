# Physics Timeline Fix TODO

Only defects verified against the current source are listed here.
Fresh full-package review pass 2026-07-06 (3 parallel reviewers over runtime, trigger/query, and authoring/data/baking; top findings re-verified by hand). Previous pass's completed items are preserved at the bottom.

## High Priority

- [ ] ChainGrabSystem can never grab — query vs enabled-state contradiction makes grab acquisition dead code.
  - Locations: `BovineLabs.Timeline.Physics/Chains/ChainGrabSystem.cs:54-57` (query) and `:119` (skip).
  - The query uses `WithAll<ChainGrabArmed>` + `WithAllRW<ChainLinkGrabbed>` with no `IgnoreComponentEnabledState`, so it only matches entities where `ChainLinkGrabbed` is ENABLED — and the loop's first line skips exactly those (`if (chunk.IsComponentEnabled(ref GrabbedHandle, i)) continue;`).
  - `ChainWeaponAuthoring` bakes `ChainLinkGrabbed` disabled, so an armed swinging link never enters the job: no Stick/Wrap/Reel joint is ever created.
  - Fix: query with `EntityQueryOptions.IgnoreComponentEnabledState` (like sibling systems) or restructure to `WithDisabled<ChainLinkGrabbed>`.
  - Add a grab-ACQUISITION regression test (the existing `ChainReleaseSystemTests` covers only release/rearm).

- [ ] Multi-winner insert overflows `FixedList64Bytes<Entity>` at the authored default `maxTargets = 7`.
  - Location: `BovineLabs.Timeline.Physics/PhysicsTriggerQuerySystem.cs:509-536` (`ConsiderMulti`).
  - `InsertRangeWithBeginEnd` runs BEFORE the trim-to-cap loop. `FixedList64Bytes<Entity>` holds exactly 7 entities; with `cap == 7` (clip default) and the list full, an 8th survivor that scores above the worst grows the list to 8 > capacity → exception in checks builds, memory corruption in players.
  - Hits every frame for any `AllSurvivorsFanout`/`TopK` clip with 8+ bodies in the volume.
  - Fix: drop the worst element before inserting when full (or return early when `insertAt >= cap` AND pre-shrink to `cap - 1`).
  - Add a test with cap 7 and 8 gated candidates.

- [ ] `WriteHitBuffer` is a silent no-op — the buffer is baked onto the clip entity but written to the routed (bound) entity.
  - Locations: `BovineLabs.Timeline.Physics.Data/Builders/PhysicsTriggerQueryBuilder.cs:22` vs `BovineLabs.Timeline.Physics/PhysicsTriggerQuerySystem.cs:1439`.
  - The builder adds `TriggerQueryHit` to the CLIP entity ("for Self-routing the clip itself needs it" — wrong: `Target.Self` resolves to `bindings[i].Value`, the bound scene entity, never the clip). `HasBuffer(evt.Routed)` then fails and every hit is dropped, including for `PhysicsAoEQueryClip` where `writeHitBuffer` + `routeTo = Self` are the advertised combo.
  - Existing tests pass only because they hand-add the buffer AND bind the clip to itself.
  - Fix: ensure the buffer on the routed/bound entity (baking system or lazily via ECB), or route hit-writes to the clip entity — pick one and make the tests bind realistically.

- [ ] Velocity-clamp blending against the default slot inverts the effect envelope — an eased clamp clip momentarily FREEZES the body.
  - Locations: `BovineLabs.Timeline.Physics.Data/VelocityClamp/PhysicsVelocityClampData.cs:38-46` (mixer), `VelocityClampKernel.cs` (`>= 0f` means active; only negative disables).
  - The blend framework fills empty slots with `default` (`MaxLinearSpeed = 0` = clamp-to-zero, the STRONGEST clamp). `lerp(0, 10, 0.1) = 1` at 10% ease weight, so the clamp is hardest exactly when the clip weight is lowest; clip declares `ClipCaps.Blending` so any ease triggers it. An authored `maxAngularSpeed = 0` also silently freezes rotation ("ignore" is negative-only per the tooltip).
  - Fix: make the blend neutral a negative sentinel (guard defaults in the mixer like `PhysicsVelocityMixer`'s `Present` flag), and/or treat 0-weight-side defaults as "no clamp".
  - Found independently by two reviewers.

- [ ] SpanStart state reset races the fixed-step exit restore — capture-restore overrides get stuck / capture poisoned.
  - Locations: `BovineLabs.Timeline.Physics/Kernels/TrackBlendDriver.cs:164-172` (render-rate `ResetStateTrackJob` on activation edge), vs the fixed-step `OnExit` paths gated on `state.Fired` in `PhysicsKinematicOverrideApplySystem`, `PhysicsGravityOverrideApplySystem`, `PhysicsFilterOverrideApplySystem`, `PhysicsShapeSwap/ResizeApplySystem`.
  - If clip A ends and clip B activates with ZERO fixed ticks in the gap (common above 60 fps with a 50/60 Hz fixed step), the reset wipes `Fired`/`Original*` before `OnExit` ever runs → restore skipped; B's `OnEnter` captures the still-overridden values as "original" (gravity stuck at 0, body stuck kinematic, `PhysicsShapeSwapState.Original` blob reference lost).
  - Fix: perform the exit-restore from the reset path itself, or defer the SpanStart reset until the apply system has consumed the exit (e.g. reset only when `!state.Fired`).
  - Found independently by two reviewers. Same defect class as the earlier PhysicsClipGate fix, but for the Active*-state family.

- [ ] `AppendVelocityJob` does not advance `AppliedTime` when the stat multiplier is ~0 — un-zeroing releases the whole backlog as one velocity spike.
  - Location: `BovineLabs.Timeline.Physics/Kinematics/PhysicsKinematicsApplySystem.cs:469`.
  - The force path handles this explicitly (`:222-231`: advances `AppliedTime` before `continue`); the velocity path just `continue`s, so `ElapsedTime - AppliedTime` accumulates and the first non-zero-stat tick delivers the entire skipped window as one Δv burst.
  - Fix: mirror the force path. Add a stat-gated AddContinuous regression test.

- [ ] `PhysicsForceMixer` has no default-slot guard on its discrete fields — an eased Impulse clip evaluates as a Continuous FixedVector force at weight < 0.5, then also fires the impulse.
  - Location: `BovineLabs.Timeline.Physics.Data/PhysicsForceData.cs:116-137`.
  - The framework injects `default(PhysicsForceData)` into empty slots; `Mode = s < 0.5f ? a.Mode : b.Mode` picks the default's `Mode = Continuous(0)`, `DirectionMode = FixedVector(0)`, `Channel = Intent(0)` etc. during the ease prelude, integrating a lerped-down force before the real impulse fires at weight ≥ 0.5.
  - Fix: add a `Present` flag (as `PhysicsVelocityData` did) or a `DiscreteMixer`-style default guard for the discrete fields.

## Medium Priority

- [ ] `PhysicsVelocityMixer.Lerp`/`Add` drop the `Present` flag from their outputs, defeating the guard on multi-fold blends.
  - Location: `BovineLabs.Timeline.Physics.Data/Mixers/PhysicsVelocityMixer.cs:13-21, 36-45`.
  - Mixed intermediates carry `Present == 0` and are misclassified as empty slots in the next fold — with 3+ overlapping velocity clips the newest clip's discrete fields win at any weight.
  - Fix: `Present = 1` (or `a.Present | b.Present`) in both returns; add a 3-clip blend test.

- [ ] Gravity-override blending uses the wrong neutral — partial weight dips toward zero-G instead of normal gravity.
  - Location: `BovineLabs.Timeline.Physics.Data/Gravity/PhysicsGravityOverrideData.cs:45`.
  - Empty-slot default has `GravityScale = 0`; the blend-neutral for a gravity SCALE is 1. An ease-in to scale 1.0 passes through 0.5 (near-weightless) at the edges.
  - Fix: default-guard in the mixer (blend toward 1 for the empty side) or a `Present` flag.

- [ ] `ClearOnLost` is ignored in multi-winner mode — routed `Targets` slots keep stale/destroyed winners.
  - Location: `BovineLabs.Timeline.Physics/PhysicsTriggerQuerySystem.cs:604-621` (`EmitMulti` lost path) vs the single-winner path (`:1091-1100`).
  - The multi lost path fires the lost condition but never emits a `WriteSlot`/`ClearOnly` event, so a slot written by fanout keeps pointing at a body that left or died.

- [ ] `GraceFrames` (LostDebounce) is off by one — a value of 1 behaves exactly like 0.
  - Location: `BovineLabs.Timeline.Physics/PhysicsTriggerQuerySystem.cs:1079-1087`. Seeded, decremented, and tested in the same frame, so the hold is `GraceFrames - 1` frames, contradicting the tooltip.

- [ ] `TabCycle` advances every query frame instead of on a re-fire edge — with Stay events it round-robins the target at frame rate.
  - Location: `BovineLabs.Timeline.Physics/PhysicsTriggerQuerySystem.cs:382-383` + `:1367-1398`. `TabCycleSuccessor` picks `lastIdx + 1` unconditionally, `LastWinner` updates, so the slot flips and `FoundCondition` spams every frame with ≥2 survivors. Needs an edge gate (advance only on a fresh activation/input, not while held).

- [ ] SocketReturn springs the weapon toward world origin when the socket `Targets` slot is empty.
  - Location: `BovineLabs.Timeline.Physics/Sockets/SocketReturnApplySystem.cs:129-137`.
  - `Targets.Get` returns `Entity.Null` for an unpopulated slot and the non-`Try` `ResolveTransform` yields `(0,0,0)/identity` — the recall spring accelerates toward the origin. Skip the entity (or hold pose) when the socket doesn't resolve.

- [ ] Stat-strength multiplier is not finite-guarded in the force, velocity-add, and drag paths (the velocity-OVERRIDE path is guarded).
  - Locations: `PhysicsKinematicsApplySystem.cs:219-222, 466-469`, `PhysicsDragApplySystem.cs:116-121` vs `PhysicsVelocityOverrideSystem.cs:140,150`.
  - A NaN/Inf stat flows into `PendingForce`/`PendingVelocity`/`exp(-drag·NaN·dt)` and permanently NaNs `PhysicsVelocity` (nothing downstream sanitizes). Mirror the override path's `math.isfinite` guards.

- [ ] `ZeroVelocityOnEnter` (kinematic override) zeroes `PhysicsVelocity` but not the `ExternalVelocity` knockback channel — a "frozen" body keeps sliding ~0.15 s.
  - Locations: `PhysicsKinematicOverrideApplySystem.cs:137-143` vs `PhysicsExternalVelocitySystem.cs` ComposeJob (unconditionally re-adds the channel every producer tick).
  - Fix: also clear `ExternalVelocity` (+ `PendingExternalForce` inbox) on enter, or make Compose skip kinematic-overridden bodies.

- [ ] Active*-driven fixed-step effects can be dropped when no fixed tick lands inside the (one-frame-delayed) enable window — the crossing-aware clip gate covers trigger clips only.
  - Locations: `SharedTrackJobs.cs:153-157` (enable via BeginSimulation ECB = next frame) vs `:87` (immediate disable); `PhysicsClipGateSystem` is consumed only by the trigger family.
  - A one-render-frame Impulse force/velocity clip at high fps can never be observed by the fixed-step apply job. Same defect class the gate fixed for triggers — extend the crossing-aware activation to the Force/Velocity/PID/Teleport apply paths.

- [ ] `SocketReturnMixer` slerps `LocalRotation` against the zero quaternion at partial weight — non-unit rotation goal distorts the spring and the `AttachAngle` arrival test.
  - Location: `BovineLabs.Timeline.Physics.Data/Sockets/SocketReturnData.cs:52`. Guard like `PhysicsAngularPIDMixer.SanitizeRotation`; note `PositionHalflife`/`AttachDistance` also lerp toward 0 (max stiffness / unreachable arrival) at blend edges.

- [ ] Teleport local-space placement ignores parent scale.
  - Location: `BovineLabs.Timeline.Physics/Teleports/TeleportPlacement.cs:46-53`. World→local inverts rotation+translation only; a parent with scale s lands the body at s× the intended offset. Divide the local position by the parent scale.

## Low Priority

- [ ] `SectorBandPacked` sentinel aliases a valid cell: `b*N + N == (b+1)*N + 0` — a co-located candidate reads as "next band, dead ahead".
  - Location: `PhysicsTriggerQuerySystem.cs:1155-1161` + `PhysicsTriggerSectorMath.cs` sentinel (`ComputeRawSector` returns `sectorCount`). Pack with `SectorCount + 1` stride or map the sentinel to a reserved value.

- [ ] `EmitMulti` hit score `(int)(scores[k] * 100f)` overflows int for distance-based selections when `MaxDistance = 0` (unlimited) or far candidates (`-distSq * 100` beyond int range → garbage/undefined in Burst).
  - Location: `PhysicsTriggerQuerySystem.cs:595`. Clamp before casting.

- [ ] First-winner routing failure suppresses the hit-buffer clear for the whole frame (stale entries accumulate up to the 8 cap).
  - Location: `PhysicsTriggerQuerySystem.cs:579-601` — `firstHit = false` runs even when `TryResolveLinkedTarget` fails for winner 0.

- [ ] FactionGate conflates "no FactionMember component" with faction 0 — untagged props are admitted/rejected by the faction-0 bit with no distinct untagged policy.
  - Location: `PhysicsTriggerQuerySystem.cs:725-736`.

- [ ] Continuous force/velocity tail is truncated at clip end — the unconsumed `ElapsedTime - AppliedTime` remainder (up to ~1 render frame + fixed-step phase) is discarded, so total impulse is still boundedly phase-dependent, contradicting the determinism note in `PhysicsForceData.cs`.

- [ ] `sectorCount` has no upper clamp at bake (`math.max(sectorCount, 1)`) but the runtime stores DirectionSector state as `sbyte` (`LastSector = (sbyte)sector`, `PhysicsTriggerQuerySystem.cs:1305`) — >127 sectors silently overflows and corrupts hysteresis. Clamp to ≤127 at bake. (PlanarGrid is already safe: cells clamped ≤120.)

- [ ] ChainFollow and SocketReturn are the only track families with no Ensure entry in `PhysicsTimelineBakingSystem` — a clip bound to an entity without `ChainWeaponAuthoring`/`WeaponRecallAuthoring` bakes cleanly and silently no-ops. Likely intentional (Active is useless without the rig), but it contradicts the system's own "add one Ensure line per new track" comment: either add the Ensure lines or document the exception.

- [ ] Delete `fix_refs.py` (+ `.meta`) from the package root — one-off namespace-rename migration script, not package content.

## Verified clean this pass (no action)

- `AssemblyInfo.cs` RegisterGenericJobType coverage is COMPLETE for all 16 driver instantiations (every TrackBlendImpl job, Prepare/DisableAbsent/WriteActive, correct reset job per RearmPolicy, AdvanceElapsedTime for Force+Velocity only).
- [BakingType]/editor-assembly landmines: `PhysicsForceAccumulatorOptOut` is the only IComponentData in the editor-only asmdefs and is already `[BakingType]`.
- All bake-time blob assets are registered via `AddBlobAsset`; degrees→radians conversions and div-by-zero guards audited clean; pending-buffer drain semantics (producer+modifier accumulators) apply exactly once; SweptTrigger exit/destroyed-entity handling sound.

## Completed (previous pass)

- [x] Fix PID targets located at world origin being treated as unresolved (`PhysicsMath.ResolveLinearPidTarget`/`ResolveAngularPidTarget`).
- [x] Respect the enabled state of `ActiveGravityOverride` in the kinematic override system (Has → IsComponentEnabled).
- [x] Reset chain link state when releasing chain anchors (rearm `ChainGrabArmed`, clear `ChainLinkGrabbed`).
- [x] Add force-accumulator buffers independently during baking (`AutoPhysicsForceAccumulatorBakingSystem`).
- [x] Validate trigger-instantiation bindings before indexing `LocalToWorldLookup`.
- [x] Wire actual clip-last-frame state into stateful event matching (`PhysicsClipGate`).
- [x] Preserve entity scale during teleport placement.
- [x] Mark `PhysicsForceAccumulatorOptOut` `[BakingType]` so player builds load (commit 6025443).
- [x] Confirm Unity successfully reloads the modified assemblies without compiler errors.

## Verification

- [ ] Run `dotnet build ../../BovineLabs.Timeline.Physics.Tests.csproj`.
  - Previously blocked in the workspace sandbox (generated project writes under the project-level `Temp/Bin`).
- [ ] Run the `BovineLabs.Timeline.Physics.Tests` EditMode suite.
