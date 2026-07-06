# Physics Timeline Fix TODO

Fresh full-package review pass 2026-07-06 (3 parallel reviewers over runtime, trigger/query, and authoring/data/baking; top findings re-verified by hand), fixed 2026-07-07.

**Status: fixed and verified — full `BovineLabs.Timeline.Physics.Tests` EditMode suite green at 184/184.**
Two structural items are deferred (see the bottom section); everything else below is done.

## High Priority — all fixed

- [x] ChainGrabSystem can never grab — query vs enabled-state contradiction made grab acquisition dead code.
  - `Chains/ChainGrabSystem.cs`: query now `.WithDisabled<ChainLinkGrabbed>()` so armed swinging links (baked with `ChainLinkGrabbed` disabled) enter the job. Job `ArmedHandle`/`GrabbedHandle` marked `[ReadOnly]` (read-only handles; all writes go through the ECB). Grab-acquisition regression test added (`ChainGrabSystemTests`).

- [x] Multi-winner insert overflowed `FixedList64Bytes<Entity>` at the authored default `maxTargets = 7`.
  - `PhysicsTriggerQuerySystem.cs` `ConsiderMulti`: drop the worst (last) survivor BEFORE inserting when the list is already at `cap`, so it never grows past capacity. Cap-7 / 8-candidate test added.

- [x] `WriteHitBuffer` was a silent no-op — buffer was baked onto the clip entity but written to the routed entity.
  - Builder no longer adds `TriggerQueryHit` to the clip; `PhysicsTriggerQuerySystem.ApplyJob` adds it to the routed/bound entity on demand. Tests bind realistically.

- [x] Velocity-clamp blending against the default slot inverted the effect envelope (an eased clamp momentarily FROZE the body).
  - `PhysicsVelocityClampData.cs`: `Present` byte flag so empty/default slots are neutral (no clamp) instead of clamp-to-zero.

- [x] SpanStart state reset raced the fixed-step exit restore — capture/restore overrides got stuck / capture poisoned.
  - `Kernels/TrackContracts.cs` adds `IRestorableState`; `TrackBlendDriver.cs` adds `TrackBlendRestorableStateDriver` whose span-start reset is constrained to `IRestorableState`, so a zero-fixed-tick clip gap can no longer wipe a pending exit restore.

- [x] `AppendVelocityJob` did not advance `AppliedTime` when the stat multiplier was ~0 — un-zeroing released the whole backlog as one velocity spike.
  - `Kinematics/PhysicsKinematicsApplySystem.cs`: velocity path now mirrors the force path (advance `AppliedTime` on a zeroed/non-finite stat for AddContinuous). Stat-gated regression test added.

- [x] `PhysicsForceMixer` had no default-slot guard on its discrete fields — an eased Impulse clip evaluated as a Continuous FixedVector force at weight < 0.5.
  - `PhysicsForceData.cs`: `Present` flag guards the discrete fields.

## Medium Priority — fixed

- [x] `PhysicsVelocityMixer.Lerp`/`Add` dropped the `Present` flag from their outputs — fixed (propagate `Present`), 3-clip blend test added.
- [x] Gravity-override blending used the wrong neutral (dipped toward zero-G) — `PhysicsGravityOverrideData.cs` default-guarded (blends toward scale 1).
- [x] `ClearOnLost` ignored in multi-winner mode — multi lost path now emits a slot clear when `ClearOnLost` is set.
- [x] `GraceFrames` (LostDebounce) off by one — seed no longer decremented on the seed frame; `1` now holds exactly one frame.
- [x] `TabCycle` advanced every query frame — now gated to the re-fire edge (prior winner preserved across held/Stay frames).
- [x] SocketReturn sprang the weapon toward world origin on an empty socket slot — now `TryResolveTransform` and skip (hold pose) when the socket doesn't resolve.
- [x] Stat-strength multiplier not finite-guarded in force / velocity-add / drag paths — `math.isfinite` guards added (kinematics ×2, drag ×1).
- [x] `ZeroVelocityOnEnter` did not clear the `ExternalVelocity` knockback channel — now cleared on enter.
- [x] `SocketReturnMixer` slerped against the zero quaternion at partial weight — `Present`-guarded / sanitized.
- [x] Teleport local-space placement ignored parent scale — `TeleportPlacement.cs` now divides the local offset by parent scale.

## Low Priority — fixed

- [x] `SectorBandPacked` sentinel aliased a valid cell — packed with `SectorCount + 1` stride reserving the top slot.
- [x] `EmitMulti` hit score `(int)(scores[k] * 100f)` overflow — `ClampScore` before the cast.
- [x] First-winner routing failure suppressed the hit-buffer clear for the whole frame — `firstHit` is consumed only once a clear-carrying event is actually written.
- [x] FactionGate conflated "no FactionMember" with faction 0 — explicit policy (untagged candidates excluded) documented in code.
- [x] `sectorCount` had no upper clamp at bake vs the `sbyte` runtime `LastSector` — clamped to `[1, 127]` in both authoring clips.
- [x] ChainFollow / SocketReturn have no Ensure entry in `PhysicsTimelineBakingSystem` — confirmed intentional (Active is useless without the `ChainWeaponAuthoring` / `WeaponRecallAuthoring` rig); documented as a deliberate exception in the baking system's summary comment.
- [x] Deleted `fix_refs.py` (+ `.meta`) — one-off migration script, not package content.

## Deferred (structural — not attempted this pass)

- [ ] Active*-driven fixed-step effects can still be dropped when no fixed tick lands inside the one-frame-delayed enable window — the crossing-aware `PhysicsClipGate` currently covers only the trigger family. Extending it to the Force/Velocity/PID/Teleport apply paths (`SharedTrackJobs` `WriteActiveJob` enables via a next-frame ECB) is the same defect class the gate fixed for triggers, but it is a larger architectural change and is left for a dedicated pass.
- [ ] Continuous force/velocity tail is truncated at clip end — the unconsumed `ElapsedTime - AppliedTime` remainder (up to ~1 render frame + fixed-step phase) is discarded, so total impulse remains boundedly phase-dependent. Inherent to the render-rate → fixed-step seam; the determinism note in `PhysicsForceData` should be softened rather than claiming exactness. Left for the same fixed-step pass as the item above.

## Verified clean this pass (no action)

- `AssemblyInfo.cs` RegisterGenericJobType coverage is COMPLETE for all driver instantiations (every TrackBlendImpl job, Prepare/DisableAbsent/WriteActive, correct reset job per RearmPolicy, AdvanceElapsedTime for Force+Velocity only). `TrackBlendRestorableStateDriver`'s jobs are registered alongside.
- [BakingType]/editor-assembly landmines: `PhysicsForceAccumulatorOptOut` is the only IComponentData in the editor-only asmdefs and is already `[BakingType]`.
- All bake-time blob assets registered via `AddBlobAsset`; degrees→radians conversions and div-by-zero guards clean; pending-buffer drain semantics apply exactly once; SweptTrigger exit/destroyed-entity handling sound.

## Completed (previous pass)

- [x] Fix PID targets located at world origin being treated as unresolved.
- [x] Respect the enabled state of `ActiveGravityOverride` in the kinematic override system.
- [x] Reset chain link state when releasing chain anchors.
- [x] Add force-accumulator buffers independently during baking.
- [x] Validate trigger-instantiation bindings before indexing `LocalToWorldLookup`.
- [x] Wire actual clip-last-frame state into stateful event matching (`PhysicsClipGate`).
- [x] Preserve entity scale during teleport placement.
- [x] Mark `PhysicsForceAccumulatorOptOut` `[BakingType]` so player builds load.
- [x] Confirm Unity reloads the modified assemblies without compiler errors.

## Verification

- [x] All physics assemblies recompile clean (0 errors) after every fix wave.
- [x] `BovineLabs.Timeline.Physics.Tests` EditMode suite: **184 passed, 0 failed, 0 skipped**.
