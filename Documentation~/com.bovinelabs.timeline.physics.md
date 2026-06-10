# BovineLabs Timeline Physics

`com.bovinelabs.timeline.physics` extends BovineLabs Timeline with physics-oriented authoring components and runtime systems for DOTS projects. It is a pure integration layer over Unity Physics: clips blend into per-target active configs, producers feed the physics step through `PendingForce`/`PendingVelocity` accumulation buffers, and modifiers run after the step. All simulation work happens inside `FixedStepSimulationSystemGroup` and is Burst-compiled and deterministic.

## System groups
- `PhysicsProducerGroup` — before `PhysicsSystemGroup` (forces, PIDs, ricochet, gravity override, trigger events)
- `PhysicsModifierGroup` — after `PhysicsSystemGroup` (velocity set, drag, clamp, teleport, filter/kinematic override)

## Tracks and clips
- Velocity: `PhysicsVelocityTrack` / `PhysicsVelocityClip` — set (post-step) or add (pre-step), instant or continuous
- Force: `PhysicsForceTrack` / `PhysicsForceClip` — continuous force or impulse, fixed vector or toward/away from a target
- Drag: `PhysicsDragTrack` / `PhysicsDragClip` — exponential velocity decay
- Velocity Clamp: `PhysicsVelocityClampTrack` / `PhysicsVelocityClampClip` — speed limits (negative limit = unlimited)
- Gravity Override: `PhysicsGravityOverrideTrack` / `PhysicsGravityOverrideClip` — stateful `PhysicsGravityFactor` override with restore-on-exit
- Kinematic Override: `PhysicsKinematicOverrideTrack` / `PhysicsKinematicOverrideClip` — stateful `PhysicsMassOverride` toggle
- Filter Override: `PhysicsFilterOverrideTrack` / `PhysicsFilterOverrideClip` — stateful collision-filter override; requires a Force Unique collider on the bound body
- PID: `PhysicsLinearPIDTrack` / `PhysicsAngularPIDTrack` with matching clips — force/torque controllers toward a target
- Teleport: `PhysicsTeleportTrack` / `PhysicsTeleportClip` — candidate-search teleport with clearance, line of sight, and a configurable azimuth reference frame
- Ricochet: `PhysicsRicochetTrack` / `PhysicsRicochetClip` — raycast bounce chain with terminal-hit condition routing
- Trigger events: `PhysicsTriggerConditionClip`, `PhysicsTriggerForceClip`, `PhysicsTriggerInstantiateClip` on `StatefulTriggerTrack`
- Chains and sockets: `ChainFollowClip`, `ChainWeaponAuthoring`, `SocketReturnClip`, `WeaponRecallAuthoring`

## Key kernels
- `PhysicsMath.ComputeFalloff` — total distance attenuation (None / Linear / InverseSquare / Step)
- `PhysicsMath.ComputePidForce`, `ComputeAngularError` — PID stepping
- `SpringMath` — critically damped position/rotation springs
- `DiscreteMixer<T>` — discrete clip blending for non-interpolable data

## Sample location
- `Sample~/Physics`

For package setup, dependency versions, and usage notes, see the package `README.md`.
