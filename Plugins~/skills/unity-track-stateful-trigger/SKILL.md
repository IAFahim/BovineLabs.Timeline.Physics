---
name: unity-track-stateful-trigger
description: Master of StatefulTriggerTrack + PhysicsTriggerInstantiateClip (package BovineLabs.Timeline.Physics, ns BovineLabs.Timeline.Physics.Authoring) — the trigger-spawn track. Bind the track to a trigger SOURCE's StatefulTriggerEventAuthoring; on the chosen Enter/Stay/Exit edge the clip spawns an ObjectDefinition payload at/aimed-at the contacted entity, with a targetLinkOverride (Essence Link) that resolves the contacted thing's Essence as the payload's Target. Owns the Raise-Trigger-Events + overlapping-collision-filter SOURCE setup that silently fails. Portable to any project containing the package; worked example (Cube Damage TRA) from vex-ee. Use when a designer says "when X enters/touches Y, spawn/do Z to Y".
---

# StatefulTriggerTrack specialist

## 1. SCOPE

You are the specialist for **`StatefulTriggerTrack`** ("BovineLabs/Physics/Stateful Trigger") and
**`PhysicsTriggerInstantiateClip`** from the package `BovineLabs.Timeline.Physics`, ns
`BovineLabs.Timeline.Physics.Authoring` — bound to a **`StatefulTriggerEventAuthoring` COMPONENT**
(ns `BovineLabs.Core.Authoring.PhysicsStates`, in the EXTERNAL `com.bovinelabs.core` package) that
lives on a trigger SOURCE object. This is the most-reused track in Arvex: the "T" of every
Trigger·Reaction·Action chain — Trap, Healing Area, AreaLock/Combat Area, Stagger Damage, Tornado,
and the canonical Cube Damage TRA all start here. Designer intent first: **"when X touches/enters Y,
make a thing happen to Y."** The track fires on contact; the clip SPAWNS a payload prefab and aims it
at the contacted thing.

Scope: authoring this track + the `PhysicsTriggerInstantiateClip`, wiring the SubScene
PlayableDirector, AND auditing/reporting the trigger SOURCE setup that the track observes (the
Raise-Trigger-Events shape + overlapping collision filter + `StatefulTriggerEventAuthoring` +
`TargetsAuthoring`). The SPAWNED payload's internals (Reaction/Action/ObjectDefinition back-link,
the −30 health, LifeCycle) are **`unity-tra-payloads`**' domain; the objdef two-way link is
**`unity-object-definitions`**'; the Owner/Source/Target/Essence-Link targeting model is
**`unity-targets`**'; whole-mechanic composition is **`unity-augment-architecture`** /
**`unity-reactions`**. Cross-reference those — do not re-derive them. (`StatefulTriggerTrack` also
accepts `PhysicsTriggerConditionClip`, `PhysicsTriggerForceClip`, `PhysicsTriggerQueryClip` —
sibling clips, OUT of this skill; this skill owns ONLY the Instantiate clip.)

Operate per `unity-timeline-track-authoring`; behave per `unity-agent-protocol`; use the editor
per `unity-cli`.

## 2. PORTABLE SEMANTICS

True in ANY project containing `BovineLabs.Timeline.Physics`. Provenance tags say where a fact was
PROVEN, not where it applies. (All verified vex-ee 2026-06 from package source — `StatefulTriggerTrack.cs`,
`PhysicsTriggerInstantiateClip.cs`, `PhysicsTriggerInstantiateSystem.cs`, `PhysicsTriggerEnums.cs`,
`PhysicsTriggerFiltering.cs`, `PhysicsTriggerInstantiateData.cs`, `StatefulEventState.cs`,
`StatefulTriggerEventAuthoring.cs`; no play mode — runtime claims are source-derived.)

| Type | Facts |
|---|---|
| `StatefulTriggerTrack` | ns + asm `BovineLabs.Timeline.Physics.Authoring`, base `DOTSTrack`, EMPTY body. `[TrackColor(0.8,0.8,0.1)]`, `[DisplayName("BovineLabs/Physics/Stateful Trigger")]`, **`[TrackBindingType(typeof(BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring))]`** — bind a `StatefulTriggerEventAuthoring` COMPONENT, NOT the Physics body, NOT the GameObject. `[TrackClipType]` ×4: `PhysicsTriggerInstantiateClip` (this skill), `PhysicsTriggerConditionClip`, `PhysicsTriggerForceClip`, `PhysicsTriggerQueryClip`. |
| `PhysicsTriggerInstantiateClip` | sealed, base `DOTSClip`, `ITimelineClipAsset`, `clipCaps => ClipCaps.None` (NO blending/looping — one config, discrete), `duration => 1` (seed only). Bake: **GUARDED** — `objectDefinition == null` ⇒ `Debug.LogError("…needs objectDefinition.")` + early return (the ONE loud bake failure). Else `DependsOn(objectDefinition)`, resolves the two `EntityLinkSchema` fields to ushort keys (null → 0), bakes `requireLinks` into a filter blob, builds `PhysicsTriggerInstantiateData` + `PhysicsTriggerFilterData` onto the clip entity. `rotationOffset` is converted DEGREES→radians at bake (`math.radians`). |
| `PhysicsTriggerInstantiateData` | clip-entity IComponentData baked verbatim: `ObjectId` (the resolved ObjectDefinition), `StatefulEventState EventState`, position/rotation modes+offsets, `Target AssignParent`, `ushort AssignParentLinkKey`, `ushort TargetLinkKey`. |
| `PhysicsTriggerFilterData` | clip-entity IComponentData: `Target IgnoreTarget`, `BlobAssetReference<PhysicsTriggerLinkBlob> LinkFilterBlob` (from `requireLinks`). |
| `StatefulTriggerEventAuthoring` | the BIND TARGET (`com.bovinelabs.core`, `…Authoring.PhysicsStates`). Its baker does ONE thing: `AddBuffer<StatefulTriggerEvent>` on the entity. That buffer is what the track reads — gated behind `#if !BL_DISABLE_PHYSICS_STATES && UNITY_PHYSICS`. No buffer ⇒ no events ⇒ the track is a silent no-op. |
| `StatefulEventState` | `: byte { Undefined=0, Enter=1, Stay=2, Exit=3 }` — quoted: Enter = interacting this frame, not last; Stay = interacting this + last; Exit = not this frame, was last. |
| Enums (clip) | `PhysicsTriggerPositionMode : byte { MatchSelf=0, MatchCollidedEntity=1, MatchContactPoint=2 }`; `PhysicsTriggerRotationMode : byte { MatchSelf=0, MatchCollidedEntity=1, AlignToContactNormal=2, Identity=3 }`. |
| System | `PhysicsTriggerInstantiateSystem` (`ISystem`, `[UpdateInGroup(PhysicsProducerGroup)]` = FixedStep, before the physics step) queries `TrackBinding + ClipActive + PhysicsTriggerInstantiateData + PhysicsTriggerFilterData`; spawns via an `EndFixedStepSimulation` ECB. Requires `ObjectDefinitionRegistry`. |

### Clip fields — serialized (camelCase) names, types, defaults from the source

| Field | Type | Default | Meaning |
|---|---|---|---|
| `objectDefinition` | ObjectDefinition | `null` | **WHAT to spawn.** Null ⇒ LogError + bake aborts. The objdef's two-way prefab link is `unity-object-definitions`' domain. |
| `triggerState` | StatefulEventState | `Enter(1)` | WHICH edge spawns: Enter (first contact frame — the common default), Stay (every contacting frame), Exit (separation frame). |
| `positionMode` | PhysicsTriggerPositionMode | `MatchContactPoint(2)` | Where the payload spawns: at the contact point / on Self / on the collided entity. |
| `positionOffset` | Vector3 | `(0,0,0)` | Added to the spawn position. |
| `positionOffsetSpace` | Target | `Self(4)` | Frame the offset is rotated through (None=raw world). |
| `rotationMode` | PhysicsTriggerRotationMode | `AlignToContactNormal(2)` | Spawn orientation: face the contact normal / match Self / match collided / Identity. |
| `rotationOffset` | Vector3 | `(0,0,0)` | Euler offset, DEGREES in YAML → radians at bake. |
| `assignParent` | Target | `None(0)` | If not None, parent the spawned payload to a resolved Target (None ⇒ unparented/world). |
| `assignParentLink` | EntityLinkSchema | `null` | Optional link hop for the parent resolution. |
| `targetLinkOverride` | EntityLinkSchema | `null` | **The "Target Override / Essence Link" field.** Assign your project's **Essence Link** schema asset here ⇒ on contact, resolve the contacted entity's Essence and set it as the payload's `Targets.Target` ("act on whatever I touched"). Null ⇒ Target = the raw contacted entity. The Target/Essence-Link model is `unity-targets`'. |
| `ignoreTarget` | Target | `Owner(3)` | Skip contacts whose ROOT matches this Targets slot — default **Owner** so a weapon never triggers on its own wielder. |
| `requireLinks` | EntityLinkSchema[] | `[]` (empty) | If non-empty, ONLY colliders publishing one of these links trigger (a whitelist). |

The designer-facing inspector labels drift from the serialized names: the wiki's **"Target Link
Override = Essence Link"** is the field `targetLinkOverride` holding the project's Essence-Link
`EntityLinkSchema` asset; **"Trigger State = Enter (1)"** is `triggerState`; **"Ignore Target = 2"**
is `ignoreTarget` (2 = `Target.Source` in the wiki's inspected setup, vs the code DEFAULT of
`Owner=3` — re-read the live value, never assume). EntityLinkSchema keys DRIFT between projects.

### Runtime semantics (source-derived)

Per fixed step (before physics), for each ACTIVE clip whose track is bound: `self =
TrackBinding.Value` (the trigger source entity). The system reads `self`'s `StatefulTriggerEvent`
buffer (and a `StatefulCollisionEvent` buffer, for non-trigger collisions). For each event it checks
`StatefulEventMatching.Matches(evt.State, EventState, isFirstFrame, isLastFrame)` — exact match, OR a
Stay event counts as Enter on the clip's first frame / Exit on its last frame. Matching events then
pass `IsValidTarget`: the contacted entity's ROOT must NOT equal the `ignoreTarget` slot's root
(default Owner — self-immunity), and if `requireLinks` is set the contacted thing must publish one of
those link keys. Survivors `Spawn`: a per-(self, other, ObjectId) dedupe set prevents double-spawn
the same step; the prefab is looked up in `ObjectDefinitionRegistry` (missing/null ⇒ `LogError("Prefab
not found for ObjectId …")`); the spawn transform is computed from position/rotation modes + contact
point/normal; `targetLinkOverride` (if set) resolves the contacted entity's linked Essence as
`Target`; then the instance's `Targets` is set `{ Owner = source's Owner ?? self, Source = source's
Source ?? self, Target = resolved-or-raw-other, Custom = source's Custom }` and the transform applied.

**Mental model:** the track is a *sensor that fires the spawner*. It does nothing itself — it spawns a
PAYLOAD that does the work. The director must run continuously (looping via `TimelineReferenceAuthoring`)
so the clip is ACTIVE when contact happens; an Enter clip placed on a one-shot non-looping director
that has already ended will never see the event.

### Traps & DO/DON'T (each source-quoted or wiki-confirmed, vex-ee 2026-06)

- **DON'T expect events without `CollisionResponse = Raise Trigger Events` on the SOURCE shape — the
  #1 silent failure.** The trigger source needs `PhysicsShapeAuthoring` with `CollisionResponse =
  RaiseTriggerEvents` (`Unity.Physics.CollisionResponsePolicy.RaiseTriggerEvents = 3`, serialized
  `3`). Anything else ⇒ `StatefulTriggerEvent` buffer stays empty ⇒ the track fires nothing, no log.
  (Collision events use the same track via the collision-event path if the shape collides normally.)
- **DON'T expect events without OVERLAPPING collision-filter categories — the #2 silent failure.**
  The source and the contacted body must mutually agree: source's `Belongs To` ∩ other's `Collides
  With` AND vice versa. Wiki Cube Weapon: Belongs-To Category05, Collides-With Category06–10; AreaLock:
  "Collides With must include PlayerBody / Category05". Wrong/no overlap ⇒ no enter events, no log.
- **DON'T bind the wrong component.** The `[TrackBindingType]` is `StatefulTriggerEventAuthoring`, NOT
  the `PhysicsBodyAuthoring`/`PhysicsShapeAuthoring` and NOT the GameObject. Bind to the source's
  `StatefulTriggerEventAuthoring`; a null/wrong binding ⇒ `self == Entity.Null` ⇒ silent skip.
- **DON'T forget `StatefulTriggerEventAuthoring` ON the source.** Its baker adds the
  `StatefulTriggerEvent` buffer the system reads. Missing/disabled ⇒ no buffer ⇒ nothing fires.
- **DO give the source `TargetsAuthoring`.** Owner = whose effect this is (usually the player Essence);
  it flows into the spawned payload's `Targets.Owner`/`Source` (filled with `self` if unset). The
  default `ignoreTarget = Owner` reads this slot for self-immunity — an unset Owner means the weapon
  has no one to ignore. The Owner/Source/Target model is `unity-targets`'.
- **DO pick `triggerState` deliberately.** Enter = once on first contact (damage on hit, enter a zone);
  Stay = every contacting frame (continuous healing/damage while inside — but each frame re-spawns a
  payload, so the payload should be cheap/self-cleaning); Exit = on leaving. Wrong phase ⇒ spawns at the
  wrong moment or never.
- **DON'T let the objdef link break.** `objectDefinition` null aborts the bake LOUDLY; but a present
  objdef whose prefab back-link is broken/duplicate-ID spawns wrong/nothing at RUNTIME
  (`LogError("Prefab not found for ObjectId …")`). The two-way link is `unity-object-definitions`'.
- **DO ensure `targetLinkOverride`'s schema resolves.** Essence Link only works if the CONTACTED
  object publishes that link (its root has an Essence reachable via the schema). A contacted thing with
  no Essence ⇒ Target falls back to the raw contacted entity, and a Reaction checking `CurrentHealth`
  finds nothing to act on. EntityLink keys drift — re-read the live schema.
- **DO keep the director looping.** `TimelineReferenceAuthoring` on the director makes the timeline
  replay so the trigger clip is always active and listening (the wiki's "Timeline always plays on a
  loop"). No loop / ended one-shot ⇒ the sensor is off when contact occurs.
- **DO note the silence profile.** Apart from the null-objdef bake LogError and the runtime
  prefab-not-found LogError, every other misconfiguration (no Raise Trigger Events, filter mismatch,
  wrong binding, missing buffer, unresolvable Essence Link) is SILENT. A clean console proves nothing.

## 3. DISCOVERY

Per `unity-timeline-track-authoring` §1 (D1–D5). D1 confirms the package
(`BovineLabs.Timeline.Physics.Authoring.StatefulTriggerTrack` / asm `BovineLabs.Timeline.Physics.Authoring`
→ `MISSING_PREREQUISITE` else). D4's bind target is
`BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring` — discover by COMPONENT, never
by name. ZERO `StatefulTriggerEventAuthoring` in the SubScene = a stage/source specialist's gap: you
BIND the source, you do NOT create it — report and stop (protocol §6).

Track-specific prerequisites to resolve the SAME read-only way and report (never improvise) if
missing — these are HALF the job and the top silent-failure causes:
- **The source SHAPE must Raise Trigger Events.** On the chosen source, read its
  `PhysicsShapeAuthoring` `CollisionResponse` — expect `RaiseTriggerEvents` (serialized `3`). Anything
  else ⇒ Gap report ("source shape isn't Raise Trigger Events — no enter events will fire").
- **Collision filter must overlap.** Read the source's `Belongs To` / `Collides With` categories and
  the intended contacted body's, and confirm a mutual overlap. No overlap ⇒ Gap.
- **The source must have `StatefulTriggerEventAuthoring` + `TargetsAuthoring`** (Owner set). Missing
  buffer ⇒ no events; unset Owner ⇒ self-immunity (`ignoreTarget`) and payload Owner/Source are unwired.
- **`objectDefinition`** needs a registered ObjectDefinition asset (and an intact prefab back-link —
  `unity-object-definitions`); **`targetLinkOverride`** needs the project's Essence-Link
  `EntityLinkSchema` — `AssetDatabase.FindAssets("t:EntityLinkSchema")`, print each name + live id/key
  (keys DRIFT — never assume; NEVER create schema assets, out of domain).

## 4. CANONICAL CLIP PATTERNS

Authored per `unity-timeline-track-authoring` §2 (the SubScene bracket). `<TRACK_TYPE>` =
`BovineLabs.Timeline.Physics.Authoring.StatefulTriggerTrack`, `<CLIP_TYPE>` =
`…PhysicsTriggerInstantiateClip`, `<BIND_TARGET>` =
`BovineLabs.Core.Authoring.PhysicsStates.StatefulTriggerEventAuthoring`. Set fields on
`(PhysicsTriggerInstantiateClip)clip.asset` via `SerializedObject` using the §2 camelCase names; assign
`objectDefinition` and `targetLinkOverride` as asset→asset object references. The PAYLOAD prefab each
pattern spawns is `unity-tra-payloads`' to build — these patterns wire only the TRIGGER half. Starts /
durations / specific assets are example CHOICES, not package constants.

- **(a) WEAPON-CONTACT DAMAGE (Cube Damage TRA).** "When my weapon touches an enemy, damage it."
  Bind the track to the weapon's `StatefulTriggerEventAuthoring`. Clip: `objectDefinition` = the damage
  payload objdef, `triggerState = Enter(1)`, `targetLinkOverride` = Essence Link (the payload damages
  the contacted Essence), `ignoreTarget = Owner` (don't hit the wielder). The payload's
  `ActionIntrinsicAuthoring` does the −30 `CurrentHealth` (`unity-tra-payloads`).
- **(b) ZONE-ENTER EFFECT (Healing Area / Trap / AreaLock).** "When something enters this zone, do Z
  to it." A static zone object carries the trigger shape + `StatefulTriggerEventAuthoring`; bind the
  track to it. Clip: `triggerState = Enter(1)`, `targetLinkOverride` = Essence Link, objdef = the
  heal/damage/wall payload. Healing Area spawns a `Healer` payload that adds `CurrentHealth +20`; Trap
  spawns a damage/`StaggerMeter` payload; AreaLock spawns a 4-wall prefab — all the same trigger wiring,
  different payload.
- **(c) STAY (continuous) vs ENTER (once).** Enter spawns once per contact begin (one hit / one entry).
  `triggerState = Stay(2)` spawns every frame the bodies overlap — for "keep healing/damaging while
  inside". Make Stay payloads cheap and self-cleaning (LifeCycle), since one spawns per frame; prefer
  Enter + a while-active payload for most continuous effects. Exit (3) fires on leaving (e.g. "spawn a
  closing effect when you leave the zone").
- **(d) RANK / LEVEL VARIANTS.** Same track + source, swap only `objectDefinition` per tier (Healing
  Area LV1→+20, LV2→+40, LV3→+60, LV4→+80; Stagger Damage 100 vs 250). The trigger wiring is identical;
  the magnitude lives entirely in the payload's Action — author one variant, change one field per rank.

## 5. WORKED EXAMPLE (delta vs the shared stage — `unity-timeline-track-authoring` §5)

Rediscover, never assume. The definitive worked example is the wiki's **Cube Damage TRA** (subscene
`Test.BovineLabs.Timeline.Animation`), NOT the generic training stage:
- **SOURCE = `Cube Weapon`** (under `Player - Arvex/.../RightSWORD/`): `PhysicsBodyAuthoring` (Dynamic,
  Gravity 1) + `PhysicsShapeAuthoring` (CollisionResponse `3` = Raise Trigger Events; Belongs-To
  Category05, Collides-With Category06–10) + `TargetsAuthoring` (Owner = player Essence, Initialize
  Target on) + `StatefulTriggerEventAuthoring`. ← the track binds HERE.
- **DIRECTOR = `Cube Stateful Check`** (under `Player - Arvex/Simulated Attack/`): `PlayableDirector`
  (timeline `Cube Stateful CheckTimeline.playable`) + `TimelineReferenceAuthoring` (loops forever).
- **TRACK + CLIP:** `StatefulTriggerTrack` bound to Cube Weapon's `StatefulTriggerEventAuthoring`;
  one `PhysicsTriggerInstantiateClip` — `objectDefinition = Cube Damage TRA`
  (`Assets/Settings/ObjectDefinitions/Cube Damage TRA.asset` → `Assets/Prefabs/Cube Damage TRA.prefab`),
  `triggerState = Enter(1)`, `targetLinkOverride = Essence Link`, `ignoreTarget = 2` (inspected value;
  code default is Owner=3 — re-read live). The PAYLOAD (`ActionIntrinsicAuthoring` −30 `CurrentHealth`,
  Reaction `CurrentHealth > 0`) is `unity-tra-payloads`'. Use the wiki's 17-point debug checklist as the
  end-to-end verification spine. On the generic training stage, `Stage_TriggerZone` (with
  `StatefulTriggerEventAuthoring`) is the natural bind target; the physics ball is the contacting body.

## 6. UNDO

Per `unity-timeline-track-authoring` §3. The runtime effect (spawned payloads, mutated health) exists
only in play mode and is never serialized — the journal reverses only AUTHORING artifacts (created
`.playable` + sub-assets, possibly-created folder, mutated `director.playableAsset`, the added generic
binding). Restore the director FIRST, then delete the asset, then any other captured values — for this
track there are NONE beyond UNDO-1 (the recipe never edits the source object, its shape/filter, the
objdef, or the schema; if discovery had to FLIP a source's CollisionResponse or add a buffer, that is a
source specialist's change, not yours — report it as a Gap, don't make it). Use the §3 UNDO-1/2/3/4
templates filled from your `PRE|` captures.

## 7. VERIFICATION

Per `unity-timeline-track-authoring` §4 (fresh-load asset dump → raw YAML → live prerequisite re-check
→ reloaded-SubScene binding → parent-scene restore → console). Track-specific expectations:
- §4.1 asset dump: `caps=None`; dump `objectDefinition` (asset ref, non-null), `triggerState`,
  `positionMode`/`rotationMode`, `targetLinkOverride` (schema ref), `ignoreTarget`, `requireLinks`.
- §4.2 YAML: enums as ints (`triggerState`, `positionMode`, `rotationMode`, `ignoreTarget`); `rotationOffset`
  in DEGREES (radians are bake-only); `objectDefinition`/`targetLinkOverride` as `{fileID:…, guid:…}`
  asset refs (a `{fileID: 0}` = the dropped-ref trap, or simply unassigned).
- §4.3 prerequisites (re-read LIVE): source `PhysicsShapeAuthoring.CollisionResponse == 3` (Raise
  Trigger Events); source + target collision-filter categories OVERLAP; source has
  `StatefulTriggerEventAuthoring` (buffer) + `TargetsAuthoring` (Owner set); objdef prefab back-link
  intact; `targetLinkOverride`'s EntityLinkSchema id/key present and the contacted thing publishes it.
- §4.4 binding: `BINDING|<trackName> (StatefulTriggerTrack) -> <sourceName> (StatefulTriggerEventAuthoring)`.
- Silence is expected, not evidence: only null-objdef (bake) and prefab-not-found (runtime) log;
  every other failure is silent.
