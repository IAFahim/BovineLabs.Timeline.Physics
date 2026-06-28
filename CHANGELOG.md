# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- **External (knockback) velocity channel.** A second motion channel so braking/drag/clamp/reset can zero a body's locomotion without eating an incoming hit (and vice versa). New `ExternalVelocity` component + `PendingExternalForce` inbox (auto-baked onto every dynamic body alongside the existing pending buffers), `PhysicsExternalVelocityComposeSystem`/`PhysicsExternalVelocityDecomposeSystem` (add-before-solver / subtract-after-solver + decay), and the `external-velocity.decay-rate` ConfigVar (default 8 ≈ a 0.15 s slide).
- `MotionChannel` enum (`Intent`/`External`) on `PhysicsTriggerForceData`, exposed by `PhysicsTriggerForceClip` (default `Intent`). `PhysicsKnockbackClip` and `PhysicsVortexClip` now default to `External` (survive braking); `PhysicsThrustClip` stays `Intent`.
- `channel` dropdown on the plain `PhysicsForceClip`/`PhysicsForceTrack` (default `Intent`) — so a designer can author "an impulse that survives braking" on a normal self-firing force clip without a trigger-collision rig. `PhysicsKinematicsApplySystem` routes the force to the external channel when set.
- `VelocityResetFlags.External` (and `All`): a velocity reset flagged `External` also wipes the knockback channel and its same-frame inbox — for parry / super-armor cancels.
- `PhysicsExternalVelocityGizmoSystem` debug drawer (`externalvelocitygizmo.draw-enabled`) visualising the channel for decay-rate tuning.
- Regression tests covering compose/decompose, knockback surviving a brake, decay-to-rest, and the same-frame External-reset parry.

### Changed
- Knockback writers (`DirectionalKnockbackSystem` sample, and `PhysicsTriggerForceClip`/`PhysicsKnockbackClip`/`PhysicsVortexClip` when their channel is `External`) deposit into the external channel instead of `PendingForce`, so they are no longer drag-braked. `PhysicsBreakForceSystem` deliberately stays on the intent channel (its impulse is a velocity-relative replacement, not additive). Knockback now decays at the global rate independent of per-body drag.
- Motion smear (`UpdateSmearVelocitySystem`) adds the external channel back when computing smear, so a knocked-back body smears at its real speed rather than walking speed.

## [1.0.2] - 2026-06-10
### Fixed
- Velocity Clamp, Gravity Override, Filter Override, Kinematic Override, and Ricochet apply systems never executed: their jobs constructed `ChunkEntityEnumerator` with a hardcoded `useEnabledMask: true` while their queries use `IgnoreComponentEnabledState`, so the (default, all-zero) enabled mask caused zero entities to be iterated. The enumerator now honors the mask validity Unity reports.
- `PhysicsTeleportClip` never baked its authored `FacingMode`; every teleport resolved facing as `FaceTarget`.
- `TeleportReferenceFrame` was declared but ignored: the resolver hardcoded `TargetToSelf` and the math discarded the argument, inverting the documented azimuth contract. All four frames are now implemented; the clip exposes the choice and defaults to `SelfToTarget` (azimuth 0° points toward the azimuth target, matching the tooltip).
- `InverseSquare` trigger-force falloff computed the square of a normalized linear ramp rather than the documented 1/r² attenuation. Falloff evaluation now lives in a single pure kernel, `PhysicsMath.ComputeFalloff`, with true inverse-square attenuation normalized at the start radius.
- `PhysicsMath.StepRicochet` clamps the direction/normal dot product before `acos`, preventing NaN grazing angles from floating-point drift; the ricochet ray direction is normalized totally (`normalizesafe`).
- `PhysicsFilterOverrideApplySystem` no longer mutates shared collider blobs (which rewrote the collision filter on every entity sharing the blob and corrupted the baked asset). Non-unique colliders are skipped with a warning directing authors to enable Force Unique on the bound body's collider.

### Changed
- Replaced the three duplicate discrete mixers (`PhysicsFilterOverrideMixer`, `PhysicsKinematicOverrideMixer`, `PhysicsRicochetMixer`) with the existing generic `DiscreteMixer<T>` primitive.
- Renamed the teleport source files to PascalCase, preserving asset GUIDs.

### Added
- `PhysicsTeleportData.ReferenceFrame` and the matching serialized clip field.
- Regression tests covering every previously dead stateful apply system, plus pure-function tests for `ComputeFalloff` and `ResolveReferenceRotation`.

## [1.0.1] - 2026-04-04
### Changed
- Refined package metadata for the Unity Package Manager.
- Rewrote the README with package purpose, track overview, and sample usage.
- Fixed the sample declaration to point at the physics sample content.
- Expanded package documentation stub.

## [1.0.0] - 2026-01-01
### Added
- Initial release.
