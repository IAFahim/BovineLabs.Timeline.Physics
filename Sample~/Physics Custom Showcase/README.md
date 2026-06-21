# Physics Custom Showcase (Non-Timeline)

The **non-timeline** counterpart to this package's Timeline samples. It exercises the
underlying `com.unity.physics.custom` authoring directly — no `PlayableDirector`, no DOTS
Timeline track — so you can see the raw physics primitives the timeline tracks are built on.

## Run it

1. Open a scene that contains a `Unity.Scenes.SubScene` (physics bodies must live in a
   SubScene to bake to ECS).
2. **Tools ▸ BovineLabs ▸ Samples ▸ Build Physics Custom Showcase**.
3. A `PhysicsCustomShowcase` root appears in that SubScene. Press **Play** to simulate.

Re-running is idempotent (it clears the previous showcase first).

## What it builds (labelled stations)

| Station | Permutations |
|---|---|
| Shapes | Box · Sphere · Cylinder · ConvexHull (Capsule/Mesh/Plane available in the API) |
| Motion types | Dynamic · Kinematic · Static |
| Materials | restitution 0.10 / 0.60 / 0.95 · low-friction slider on a tilted ramp |
| Collision response | `RaiseTriggerEvents` zone · `CollideRaiseCollisionEvents` body |
| Joints | BallAndSocket pendulum · LimitedHinge **edge-hinged** door · LimitedDistance hanging chain |

## The joint gotcha this sample gets right

`BaseJoint.PositionLocal` defaults to **(0,0,0) = the body's center**. Left at the default
(with `AutoSetConnected`) the door spins about its own centerline instead of its edge, and a
distance chain anchored center-to-center crumbles. This sample sets the real pivots:

- **Door** → `PositionLocal = (-1.2, 0, 0)` (the hinge edge, placed at the static frame).
- **Chain links** → `PositionLocal = (0, +radius, 0)` (the link's top end), links spaced by
  their diameter so they hang end-to-end.
- **Pendulum** → `ConnectedBody = null` (world anchor) + explicit `PositionInConnectedEntity`;
  here a center anchor is correct.
