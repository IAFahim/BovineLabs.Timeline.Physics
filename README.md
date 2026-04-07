# BovineLabs Timeline Physics

BovineLabs Timeline Physics adds physics-focused Timeline tracks for DOTS projects built on top of BovineLabs Timeline Core.

It currently provides:
- Physics Velocity track and clip authoring for driving `PhysicsBodyAuthoring` velocity over time
- Stateful Trigger track and clip authoring for instantiating prefabs from trigger state changes

## Package name

`com.bovinelabs.timeline.physics`

## Dependencies

This package depends on:
- `com.bovinelabs.core`
- `com.bovinelabs.timeline`
- `com.bovinelabs.timeline.core`
- `com.unity.burst`
- `com.unity.collections`
- `com.unity.entities`
- `com.unity.mathematics`
- `com.unity.physics`
- `com.unity.timeline`

See `package.json` for exact versions.

## Tracks

### Physics Velocity
- Track: `BovineLabs/Timeline/Physics/Velocity`
- Binding: `PhysicsBodyAuthoring`
- Purpose: apply timeline-authored velocity data to a bound physics body

### Stateful Trigger Instantiate
- Track: `BovineLabs/Timeline/Physics/Stateful Trigger`
- Purpose: spawn a prefab when a selected trigger state is observed
- Clip fields:
  - `prefab`
  - `eventState`
  - `snapToTransform`

## Sample

The package includes a sample at:
- `Sample~/Physics`

Import it through the Unity Package Manager Samples UI to inspect the package structure and timeline assets used for validation.

## Development

This package is intended to be tested from a host Unity project by embedding or referencing it as a package dependency and validating Timeline playback in-scene.
