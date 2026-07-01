# Motion Channels — designer guide

**The one thing to remember:** every force/knockback you author lands on one of two channels.
Pick the right one with the **`channel`** dropdown and your braking behaves the way players expect.

| Channel | Use it for | Your own drag / velocity-clamp / reset… |
|---|---|---|
| **Intent** (default) | the character's *own* movement — dashes, thrust, lunges, recoil you want to feel "grounded" | …**shape it**. A brake clip slows/stops it. This is what you want for locomotion. |
| **External** | a *hit landing on you* — knockback, launches, a shove, a vortex pulling you in | …**can't touch it**. The hit flies even while you're air-braking, then fades on its own. |

The bug this fixes: a move that brakes to a precise stop (drag/clamp clip) used to also **eat an
incoming knockback** — you'd get hit mid-brake and barely move. Put the knockback on **External**
and the brake leaves it alone.

## How to author it

- **Force track → Punch/dash clip (`PhysicsForceClip`):** set the **`channel`** dropdown.
  `Intent` for your own dashes; `External` for a hit you apply to a victim.
- **Already wired for you:** `PhysicsKnockbackClip` and `PhysicsVortexClip` are **External** by
  default; `PhysicsThrustClip` is **Intent**. `PhysicsTriggerForceClip` also has the `channel` dropdown.
- **Parry / super-armor:** on a force clip's **`resetVelocityOnFire`**, the **`External`** flag
  *clears* an incoming hit — author this on the move that should hard-cancel knockback.
- Knockback fades at a global rate (`external-velocity.decay-rate`, default 8 — higher = snappier).
  You don't add drag to stop a knockback; it stops itself.

## This showcase

Open **MotionChannelShowcase.unity** and press **Play**.

Two balls, **identical** heavy air-brake running the whole time, **identical** sideways punch at
t≈0.6s. The *only* difference is the punch clip's `channel`:

- 🔵 **INTENT** — the brake eats the punch → barely leaves its start marker.
- 🔴 **EXTERNAL** — the punch flies straight through the brake → travels many times farther, then decays.

Open either lane's **`Punch`** clip in the Timeline window to see the `channel` dropdown that is the
whole difference. Re-enter Play to replay. Rebuild anytime via menu **Showcase ▸ Build Motion Channels**.
