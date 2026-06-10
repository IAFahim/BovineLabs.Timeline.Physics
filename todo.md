# Physics Timeline Fix TODO

Only defects verified against the current source are listed here.

## High Priority

- [x] Fix PID targets located at world origin being treated as unresolved.
  - Locations: `BovineLabs.Timeline.Physics/Infrastructure/PhysicsMath.cs:295` and `:338`.
  - `ResolveLinearPidTarget` and `ResolveAngularPidTarget` use `math.lengthsq(targetPos) < 1e-6f` as an invalid-target test.
  - A valid target at `(0, 0, 0)` therefore falls back to the controlled entity's transform.
  - Determine validity from entity/component lookup success, not position.
  - Add linear and angular PID regression tests with a valid target at world origin.

- [x] Respect the enabled state of `ActiveGravityOverride` in the kinematic override system.
  - Locations: `BovineLabs.Timeline.Physics/Kinematics/PhysicsKinematicOverrideApplySystem.cs:100`, `:138`, `:171`, and `:196`.
  - `chunk.Has(ref ActiveGravityOverrideHandle)` only tests whether the enableable component exists.
  - Baking adds this component disabled, so its mere presence prevents `ZeroGravity` from applying and restoring.
  - Check `chunk.IsComponentEnabled(ref ActiveGravityOverrideHandle, i)` per entity.
  - Add a regression test where an entity has a disabled `ActiveGravityOverride` and an active zero-gravity kinematic override.

- [x] Reset chain link state when releasing chain anchors.
  - Locations: `BovineLabs.Timeline.Physics/Chains/ChainGrabSystem.cs:118`, `:135-136`, and `:244-255`.
  - Grabbing enables `ChainLinkGrabbed` and disables `ChainGrabArmed`.
  - Releasing destroys joints and clears anchors, but never disables `ChainLinkGrabbed` or rearms affected links.
  - Released links remain permanently blocked by the grabbed-state check and cannot grab again.
  - Track the affected link for each anchor, then restore both enableable states on release.
  - Add a grab, release, and re-grab regression test.

- [x] Add force-accumulator buffers independently during baking.
  - Location: `BovineLabs.Timeline.Physics.Authoring/AutoPhysicsForceAccumulatorBakingSystem.cs:35-41`.
  - The query excluded `PhysicsForceAccumulatorOptOut` and entities with `PendingForce`, then added both
    `PendingForce` and `PendingVelocity`.
  - An entity with only `PendingForce` never receives `PendingVelocity`.
  - An entity with only `PendingVelocity` is queued for a duplicate `PendingVelocity` addition.
  - Check and add each missing buffer independently.
  - Add baking tests for entities with neither buffer and with either single buffer already present.

## Medium Priority

- [x] Validate trigger-instantiation bindings before indexing `LocalToWorldLookup`.
  - Locations: `BovineLabs.Timeline.Physics/TriggerEvents/PhysicsTriggerInstantiateSystem.cs:183`, `:196`, and `:213`.
  - The system validates the collided entity's transform but directly indexes `LocalToWorldLookup[self]`.
  - A null binding or a bound entity without `LocalToWorld` can cause an invalid lookup access.
  - Skip invalid bindings before reading targets or event buffers, matching the guard used by `PhysicsTriggerForceSystem`.
  - Add tests for null and transform-less bindings.

- [x] Wire actual clip-last-frame state into stateful event matching.
  - Locations: `BovineLabs.Timeline.Physics.Data/TriggerEvents/PhysicsTriggerEnums.cs:23-30` and every production call to `StatefulEventMatching.Matches`.
  - The matcher supports translating `Stay` to `Exit` on the clip's last frame.
  - All condition, force, and instantiate callers pass `false` for `isClipLastFrame`, so that path is unreachable.
  - Compute and pass the real last-frame state, or remove the unsupported synthetic-exit behavior.
  - Add regression coverage for an `Exit` action when the underlying physics event is still `Stay` as the clip ends.

- [x] Preserve entity scale during teleport placement.
  - Locations: `BovineLabs.Timeline.Physics/Teleports/TeleportPlacement.cs:21` and `:30`.
  - `LocalTransform.FromPositionRotation` and the parented `TRS(..., 1f)` path force scale to `1`.
  - Applying the returned transform replaces the teleported entity's existing `LocalTransform`, unexpectedly resetting its scale.
  - Preserve the current local scale while changing position and rotation.
  - Add unparented and parented teleport tests using a non-unit scale.

## Verification

- [x] Confirm Unity successfully reloads the modified assemblies without compiler errors.
- [ ] Run `dotnet build ../../BovineLabs.Timeline.Physics.Tests.csproj`.
  - Blocked in the workspace sandbox because the generated project writes under the project-level `Temp/Bin`.
- [ ] Run the `BovineLabs.Timeline.Physics.Tests` EditMode suite.
