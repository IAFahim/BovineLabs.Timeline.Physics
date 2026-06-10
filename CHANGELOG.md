# Changelog

All notable changes to this project will be documented in this file.

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
