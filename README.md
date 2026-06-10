# BovineLabs Timeline Physics

Physics-focused Timeline tracks for DOTS projects built on BovineLabs Timeline Core and Unity Physics.

The package is a pure integration layer: it never reimplements the physics step. Every feature reduces to one of three orthogonal primitives, evaluated deterministically inside `FixedStepSimulationSystemGroup`:

1. **Track blending** (`TimelineComponentAnimationGroup`) — clips blend authored data through an `IMixer<T>` into a per-target `Active*` enableable component. Discrete (non-interpolable) data uses the shared `DiscreteMixer<T>`.
2. **Producers** (`PhysicsProducerGroup`, before the physics step) — convert active configs into `PendingForce` / `PendingVelocity` buffer entries or component writes. Accumulation buffers are drained in deterministic chunk order by the force accumulator.
3. **Modifiers** (`PhysicsModifierGroup`, after the physics step) — post-step overrides such as velocity set, drag, and clamping.

Stateful overrides (gravity, kinematic, collision filter, clamp, ricochet, teleport) follow an enter/apply/exit machine driven by the enabled bit of their `Active*` component, capturing original values on enter and restoring them on exit.

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
