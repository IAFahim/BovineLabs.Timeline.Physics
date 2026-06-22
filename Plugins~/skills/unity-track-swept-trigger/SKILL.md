---
name: unity-track-swept-trigger
description: Master of SweptTriggerTrack + SweptTriggerSourceAuthoring (package BovineLabs.Timeline.Physics, ns BovineLabs.Timeline.Physics.Authoring) — the SWEPT companion to StatefulTriggerTrack for animation-driven melee weapons (a swung blade). It traces a "dummy" capsule from the body's previous to current pose each active frame and writes the SAME StatefulTriggerEvent buffer the simulation-driven trigger uses, so the EXACT same clips (Instantiate / Condition / Force / BreakForce / Query) fire — but detection is swept (with auto-densified rotation sub-steps) so a fast swing is far less likely to tunnel past a thin target. Owns the empty-CollidesWith silent-no-op, the Enter-not-Stay-on-fast-swings rule, the entity-granular (not collider-key) events, and the clip-must-be-active gate. Portable to any project containing the package; worked example (WireOnPlayer) from vex-ee. Use when a designer says "when my SWINGING weapon hits X, spawn/do Z" (vs a static overlap zone → use unity-track-stateful-trigger).
---

# SweptTriggerTrack specialist

## 1. SCOPE

You own **`SweptTriggerTrack`** ("BovineLabs/Physics/Swept Trigger") + its binding component
**`SweptTriggerSourceAuthoring`**, package `BovineLabs.Timeline.Physics`, ns
`BovineLabs.Timeline.Physics.Authoring`. It is the **drop-in companion** to `StatefulTriggerTrack`
(see `unity-track-stateful-trigger`): it hosts the **same five clip types** (Instantiate, Condition,
Force, BreakForce, Query) and writes the **same `StatefulTriggerEvent` buffer**, so every existing
trigger clip works unchanged. The ONLY difference is HOW contacts are detected:

| | StatefulTrigger (sim) | SweptTrigger (this) |
|---|---|---|
| Detection | physics simulation overlap events | a capsule **swept** prev→cur pose each frame via `CollisionWorld.CastCollider` + an overlap pass |
| Binds to | `StatefulTriggerEventAuthoring` (a real `RaiseTriggerEvents` collider) | `SweptTriggerSourceAuthoring` (a "dummy" capsule defined by params) |
| Good for | static volumes, overlap zones, slow bodies | **animation-driven weapons** (a swung blade) that would tunnel or sit at the wrong place as a real collider |
| Fast-swing | tunnels / misses | catches it (swept, sub-stepped) |

**When to choose Swept:** the weapon's pose is driven by an ANIMATION (Rukhanka) or a fast motion,
and a collider stuck on it either tunnels through thin targets or — if nested under the character's
physics body — bakes into a static compound that never follows the bone. That static-compound trap is
the whole reason this track exists.

Operate per `unity-timeline-track-authoring` and `unity-agent-protocol`; use the editor per `unity-cli`.

## 2. THE DESIGNER SETUP (zero code)

1. **On the weapon** (a small GameObject parented to the hand/weapon bone — its animated world
   transform IS the sweep path): add **`SweptTriggerSourceAuthoring`**. Set:
   - `radius` / `length` / `offset` / `axis` — the capsule down the blade. **A Scene gizmo draws it
     while the object is selected** (blue = ok, RED = CollidesWith empty), so size it visually.
   - `collidesWith` — the categories the sweep may hit. **Defaults to Everything**; for clean
     single-target melee NARROW it to your enemy category (see trap §3.1).
   - `subSteps` — leave 1; raise only for extremely fast spins.
   - Also add **`TargetsAuthoring`** with `Owner` = the wielder, so `ignoreTarget = Owner` on the clip
     skips the character itself.
2. **Timeline:** add a **Swept Trigger** track. Drag the SAME clips you'd use on a Stateful Trigger
   track (Instantiate to spawn, Condition to fire an event, Force to knock back, etc.).
3. **Bind** the Swept Trigger track to the weapon's `SweptTriggerSourceAuthoring`.
4. **Scope the clip to the attack's active frames** (the swing's contact window). The sweep only runs
   while a clip on the track is **active** (`ClipActive`) — that IS your active-frame gate.

## 3. TRAPS (each is a SILENT no-op / wrong result — no console error)

1. **Empty `CollidesWith` → hits nothing.** A sweep whose filter has no categories detects zero
   bodies. Now defaulted to Everything + an `OnValidate` warning + a RED gizmo, but if you clear it
   you get silent nothing. Conversely, **Everything is too broad**: most scene colliders default
   `BelongsTo = everything`, so the sweep will report extra hits (the `hits=2` you'll see). For
   precise melee, put enemies on a DEDICATED category and set `collidesWith` to ONLY that.
2. **Fast swings emit Enter, rarely Stay.** A swung blade passes a target in ~1 frame → you get
   Enter→Exit with little/no Stay. **Use `triggerState = Enter`** on swept clips (Condition/Query/
   Force). A clip set to `Stay` (the Query clip's default!) will almost never fire on a swing.
3. **The clip-active gate.** No active clip on the Swept track this frame → no sweep at all. If your
   clip's time window doesn't overlap the swing's contact frames, the blade passes through with no
   hits. Align the clip to the active frames.
4. **`MatchContactPoint` spawns in MID-AIR on swept clips.** Swept events are TRIGGER events with NO
   true contact point, but `Instantiate.positionMode` defaults to `MatchContactPoint` and `Force`'s
   radial origin assume one — so they silently fall back to the **midpoint between the blade centre and
   the target centre**. The spawned VFX/projectile then appears floating halfway between weapon and
   victim, looking disconnected ("a sphere comes first, away from the blade"). **For swept melee set
   `positionMode = MatchCollidedEntity`** (spawn ON the victim) or `MatchSelf` (at the blade). Only use
   `MatchContactPoint` on the simulation `StatefulTriggerTrack`, whose events DO carry a contact point.
   Exact edge placement on swept would need the system to emit `StatefulCollisionEvent` with the cast
   Position/Normal (programmer task).
5. **Animation drives the swing — you do NOT add a movement track.** The bone-anchored weapon already
   follows the Rukhanka animation. Do not add a Position/PID track to "make it swing". Just put the
   Swept Trigger clip on the attack's active window. (The vex-ee worked example uses a long
   always-active clip because the idle-swing loops forever; for a real attack, scope it to the frames.)
6. **Seeing the sweep at runtime (Quill).** The `SweptTriggerDebugSystem` draws the capsule + sweep
   path + hits — enable ConfigVar `sweptgizmo.draw-enabled` (plus Quill's `draw.enabled-global`). It
   only draws when the body and Quill's DrawSystem are in the SAME world, i.e. the **subscene must be
   STREAMED (closed), not open-for-editing**. The author-time capsule gizmo (§2) does NOT need this.

## 4. WORKED EXAMPLE (vex-ee) — `WireOnPlayer`

`Assets/Editor/SweptTriggerTest/SweptTriggerTestBuilder.cs` → menu **Showcase/Wire Swept On Player**
puts a `SweptTriggerSourceAuthoring` blade on the Player rig's `mixamorig:RightSWORD` bone (Owner =
Player), a target capsule in front, and a director with an Instantiate clip on a Swept Trigger track.
Verified live: the idle-swing animation sweeps the blade through the target and spawns the
ObjectDefinition on each pass. Menu **Showcase/Build Swept Trigger Test** builds an isolated dummy rig
(driven swing) with Instantiate + Force + Query cells.

VERIFY (streamed subscene strips entity names — query by COMPONENT, not name): count entity growth for
spawns; read the source's `SweptTriggerState.WasActive` + `SweptTriggerHit` buffer; the swept source is
the entity carrying `SweptTriggerConfig`.

## 5. STILL-OPEN DESIGNER SCAFFOLDING (not yet built)

- An `ensure_swept_source` CLI tool (parallel to `EnsureTriggerSourceTool`) to validate
  collidesWith / TargetsAuthoring / sizes.
- A timeline overlay showing the clip-active window vs the animation's swing frames.
- A "Setup Swept Weapon" context menu that adds the component + TargetsAuthoring + a pre-bound track.
