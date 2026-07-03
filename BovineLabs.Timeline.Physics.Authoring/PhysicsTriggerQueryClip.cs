using System;
using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerQueryClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema routeLink;
        public EntityLinkSchema passLaneRefLink;
        public EntityLinkSchema blockingRefLink;

        public StatefulEventState triggerState = StatefulEventState.Stay;
        public PhysicsCategoryTags collidesWith;

        [Header("Gates")] [Tooltip("Maximum query distance. 0 = unlimited.")]
        public float maxDistance;

        [Tooltip("Maximum angle from the bound entity's forward, in degrees. 0 = ignore.")] [Range(0f, 180f)]
        public float maxAngleDegrees;

        public bool requireLineOfSight;
        public PhysicsCategoryTags obstacles;
        public float lineOfSightOffset = 0.5f;

        [Header("Selection")] public PhysicsTriggerQuerySelection selection = PhysicsTriggerQuerySelection.Nearest;

        [Tooltip("StickyWinner: a challenger only displaces the incumbent winner when its score beats the incumbent's "
                 + "re-scored score by this margin (×100). 0 = no stickiness (legacy).")]
        public int switchMargin;

        [Header("Routing")]
        [Tooltip("Whose Targets slot receives the winner. Self routes to the bound entity itself.")]
        public Target routeTo = Target.Self;

        [Tooltip("Which slot on the routed entity the winner is written into.")]
        public PhysicsTriggerRouteSlot routeSlot = PhysicsTriggerRouteSlot.Custom;

        [Tooltip("How the routed slot is written. SetIfEmpty never clobbers an authored target.")]
        public PhysicsTriggerWriteMode writeMode = PhysicsTriggerWriteMode.Set;

        [Tooltip("When all candidates are lost, write Entity.Null into the routed slot.")]
        public bool clearOnLost = true;

        [Tooltip("LostDebounce: on winner loss, hold LastWinner for this many query frames before firing the lost "
                 + "condition / clearing. A reappearing winner cancels the pending lost. 0 = clear immediately (legacy).")]
        public int graceFrames;

        [Header("Value")]
        [Tooltip("How the int payload of the fired condition is computed from the winner.")]
        public PhysicsTriggerQueryValueMode valueMode = PhysicsTriggerQueryValueMode.Constant;

        [Tooltip("DirectionSector bin count (4 / 8 / 16).")]
        public int sectorCount = 8;

        public PhysicsTriggerSectorReference sectorReference = PhysicsTriggerSectorReference.SelfForward;
        public PhysicsTriggerSectorPlane sectorPlane = PhysicsTriggerSectorPlane.XZ;
        public Vector3 sectorCustomUp = Vector3.up;

        [Tooltip("Schmitt deadband in radians around a sector boundary. <= 0 uses ~0.15 * binWidth.")]
        public float sectorHysteresis = -1f;

        [Tooltip("DistanceBand: ascending distance thresholds (metres). distSq is bucketed against their squares.")]
        public float[] distanceBands = Array.Empty<float>();

        [Header("Conditions (Optional)")] public ConditionEventObject foundCondition;
        public int foundValue = 1;
        public ConditionEventObject lostCondition;
        public int lostValue = 1;

        [Header("Filtering")] [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links are considered.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        // ---------------------------------------------------------------------------------------------------
        // WAVE 2
        // ---------------------------------------------------------------------------------------------------

        [Header("Wave 2 — Gating")]
        [Tooltip("ExcludeRoles: skip any candidate that is a routed role of this query (broader than ignoreTarget).")]
        public PhysicsTriggerRoleMask excludeRoles = PhysicsTriggerRoleMask.None;

        [Tooltip("Chainable (AND'd) gate flags.")]
        public PhysicsTriggerGateFlags gates = PhysicsTriggerGateFlags.None;

        [Tooltip("ApproachGate: keep only candidates closing faster than this (m/s).")]
        public float minClosingSpeed;

        [Tooltip("ApproachGate polarity: keep receding bodies instead of closing ones.")]
        public bool approachRecedingOnly;

        [Tooltip("FacingGate: cos threshold. Default 0.5 ≈ 60° cone.")]
        public float facingCosThreshold = 0.5f;

        [Tooltip("FacingGate polarity: require the candidate to FACE self (face-to-face) instead of back-turned.")]
        public bool facingFaceToFace;

        [Tooltip("VerticalGate: offset.y below this is Grounded.")]
        public float verticalMidLow = -0.5f;

        [Tooltip("VerticalGate: offset.y above this is Aerial.")]
        public float verticalMidHigh = 0.5f;

        [Tooltip("VerticalGate allowed tiers: bit0 Grounded, bit1 Mid, bit2 Aerial.")]
        public int verticalTierMask = 0b111;

        [Tooltip("FrameWindowGate: clip-normalized [0,1] sub-range where acquisition is allowed.")]
        public float frameWindowStart;

        public float frameWindowEnd = 1f;

        [Tooltip("MassBracket: inclusive InverseMass bracket.")]
        public float massInvMin;

        public float massInvMax = 100f;

        [Tooltip("MassBracket: include static bodies (no PhysicsMass → InverseMass 0).")]
        public bool massIncludeStatic;

        [Tooltip("FactionGate: allowed (1 << faction) bits. Games bind FactionMember on their bodies.")]
        public int factionAllowMask;

        [Header("Wave 2 — Selection (Threat)")]
        [Tooltip("HighestThreat / WeakestTarget Essence stat read from the CANDIDATE.")]
        public StatSchemaObject threatStat;

        public float threatWeightDist = 1f;
        public float threatWeightAlign;
        public float threatWeightStat = 1f;

        [Tooltip("DwellSelect: descending = longest-committed, ascending (off) = freshest contact.")]
        public bool dwellDescending;

        [Tooltip("ClosingSpeedSelect: pick fastest fleeing instead of fastest incoming.")]
        public bool closingSpeedFleeing;

        [Tooltip("CategoryPriority / CategoryOrdinal: BelongsTo masks (per tier).")]
        public PhysicsCategoryTags[] categoryTierMasks = Array.Empty<PhysicsCategoryTags>();

        [Tooltip("CategoryPriority / CategoryOrdinal: ordinal for the matching tier (parallel to masks).")]
        public int[] categoryTierOrdinals = Array.Empty<int>();

        [Header("Wave 2 — Multi-winner")]
        [Tooltip("AllSurvivorsFanout / TopK hard cap (1..7). Survivors past the cap are dropped.")]
        [Range(1, 7)]
        public int maxTargets = 7;

        [Tooltip("Also emit a capped DynamicBuffer<TriggerQueryHit> on the routed entity.")]
        public bool writeHitBuffer;

        [Header("Wave 2 — Value")]
        [Tooltip("ScaledMagnitude Essence stat read from the winner.")]
        public StatSchemaObject scaledMagnitudeStat;

        [Tooltip("ScaledMagnitude: ascending magnitude thresholds (NOT distances) → band int.")]
        public float[] magnitudeBands = Array.Empty<float>();

        [Tooltip("ApproachVelocityBand: m/s per band step.")]
        public float approachBandWidth = 2f;

        [Tooltip("OverlapCount: emit 1 only on the frame the survivor count crosses up past the threshold.")]
        public bool overlapThresholdCross;

        public int overlapThreshold = 3;

        [Header("Wave 2 — Route")]
        [Tooltip("ByValue routes into a slot chosen by the computed Value (0→Custom,1→Owner,2→Source,3→Target).")]
        public PhysicsTriggerRouteMode routeMode = PhysicsTriggerRouteMode.Fixed;

        [Tooltip("MirrorIntoWinner: also write self into the winner's slot (single-threaded ApplyJob).")]
        public bool mirrorIntoWinner;

        [Header("Wave 2 — Stability / Budget")]
        [Tooltip("PerTargetRefractory: a body that won can't re-win for this many query frames.")]
        public int perTargetRefractoryFrames;

        [Tooltip("LoS family raycast budget per query. 0 = unlimited.")]
        public int maxRaycastsPerQuery;

        // ---------------------------------------------------------------------------------------------------
        // WAVE 3
        // ---------------------------------------------------------------------------------------------------

        [Header("Wave 3 — Gating")]
        [Tooltip("DraftCorridorGate: how far behind a leader (-its forward) the slipstream reaches (m).")]
        public float draftCorridorLength = 4f;

        [Tooltip("DraftCorridorGate: max perpendicular distance from the leader's rear line (m).")]
        public float draftCorridorRadius = 1f;

        [Tooltip("LedgeGate: down-ray length from the candidate. A ground hit within this depth excludes it.")]
        public float ledgeRayDepth = 1.5f;

        [Tooltip("PassLaneCone: cos half-angle of the second cone aimed at the reference target. Default 0.5 ≈ 60°.")]
        public float passLaneConeCos = 0.5f;

        [Tooltip("PassLaneCone: which role is the cone's reference (axis = refPos - selfPos).")]
        public Target passLaneRefTarget = Target.Target;

        [Tooltip("ZoneStateGate: invert (exclude tagged candidates instead of requiring the tag).")]
        public bool zoneStateInvert;

        [Tooltip("SurfaceMaterialGate: allowed contact-material/custom-tag bits. 0 = passthrough.")]
        public PhysicsCategoryTags surfaceMaterials;

        [Tooltip("LightExposureGate: illumination threshold the candidate's TriggerQueryExposure.Value is compared to.")]
        public float lightExposureThreshold = 0.5f;

        [Tooltip("LightExposureGate: invert (keep darker than threshold instead of brighter).")]
        public bool lightExposureInvert;

        [Header("Wave 3 — Selection")]
        [Tooltip("HeaviestMover: mass exponent a in mass^a * |v|^b.")]
        public float heaviestMassExp = 1f;

        [Tooltip("HeaviestMover: speed exponent b. 0 = pure mass; 1 = momentum.")]
        public float heaviestSpeedExp = 1f;

        [Tooltip("MostBlocking: the far endpoint of the self→reference segment (perpendicular distance scored).")]
        public Target blockingRefTarget = Target.Target;

        [Header("Wave 3 — Value")]
        [Tooltip("ImpactBand: ascending collision-impulse thresholds → band int.")]
        public float[] impactBands = Array.Empty<float>();

        [Tooltip("TimingWindowGrade: authored beat centre in clip-normalized [0,1].")]
        [Range(0f, 1f)]
        public float timingBeatCenter = 0.5f;

        [Tooltip("TimingWindowGrade: |t-beat| ≤ this → Perfect (0).")]
        public float timingPerfect = 0.05f;

        [Tooltip("TimingWindowGrade: ≤ this → Great (1).")]
        public float timingGreat = 0.12f;

        [Tooltip("TimingWindowGrade: ≤ this → Good (2); else Late (3).")]
        public float timingGood = 0.25f;

        [Header("Wave 3 — Route")]
        [Tooltip("RedirectToLinkedRole: the winner's outbound EntityLink (pet → master) the winner is redirected through.")]
        public EntityLinkSchema redirectLink;

        [Header("Wave 3 — Stability")]
        [Tooltip("DwellToAcquire: a candidate must survive gating this many consecutive frames before it can win.")]
        public int dwellToAcquireFrames;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            if (redirectLink == null || !EntityLinkAuthoringUtility.TryGetKey(redirectLink, out var redirectKey))
                redirectKey = 0;

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);
            var distanceBandBlob = PhysicsTriggerBakingUtility.BakeDistanceBandBlob(context.Baker, distanceBands);
            var magnitudeBandBlob = PhysicsTriggerBakingUtility.BakeMagnitudeBandBlob(context.Baker, magnitudeBands);
            var impactBandBlob = PhysicsTriggerBakingUtility.BakeMagnitudeBandBlob(context.Baker, impactBands);

            // Wave 2 category tier list.
            var tierMasks = new uint[categoryTierMasks.Length];
            for (var m = 0; m < categoryTierMasks.Length; m++) tierMasks[m] = categoryTierMasks[m].Value;
            var categoryTierBlob =
                PhysicsTriggerBakingUtility.BakeCategoryTierBlob(context.Baker, tierMasks, categoryTierOrdinals);

            var resolvedHysteresis = sectorHysteresis >= 0f
                ? sectorHysteresis
                : PhysicsTriggerSectorMath.DefaultHysteresis(math.max(sectorCount, 1));

            var builder = new PhysicsTriggerQueryBuilder
            {
                QueryData = new PhysicsTriggerQueryData
                {
                    EventState = triggerState,
                    CollidesWithMask = collidesWith.Value,
                    MaxDistance = maxDistance,
                    MaxAngle = math.radians(maxAngleDegrees),
                    RequireLineOfSight = requireLineOfSight,
                    ObstacleMask = obstacles.Value,
                    LineOfSightOffset = lineOfSightOffset,
                    Selection = selection,
                    RouteTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, routeLink, routeTo),
                    ClearOnLost = clearOnLost,
                    FoundCondition = foundCondition ? foundCondition.Key : ConditionKey.Null,
                    FoundValue = foundValue,
                    LostCondition = lostCondition ? lostCondition.Key : ConditionKey.Null,
                    LostValue = lostValue,

                    ValueMode = valueMode,
                    SectorCount = math.max(sectorCount, 1),
                    SectorReference = sectorReference,
                    SectorPlane = sectorPlane,
                    SectorCustomUp = sectorCustomUp,
                    SectorHysteresis = resolvedHysteresis,
                    DistanceBands = distanceBandBlob,

                    RouteSlot = routeSlot,
                    WriteMode = writeMode,

                    SwitchMargin = switchMargin,
                    GraceFrames = (ushort)math.clamp(graceFrames, 0, ushort.MaxValue),

                    // ---- WAVE 2: GATING ----
                    ExcludeRoles = excludeRoles,
                    Gates = gates,
                    MinClosingSpeed = minClosingSpeed,
                    ApproachRecedingOnly = approachRecedingOnly,
                    FacingCosThreshold = facingCosThreshold,
                    FacingFaceToFace = facingFaceToFace,
                    VerticalMidLow = verticalMidLow,
                    VerticalMidHigh = verticalMidHigh,
                    VerticalTierMask = (byte)(verticalTierMask & 0x7),
                    FrameWindowStart = frameWindowStart,
                    FrameWindowEnd = frameWindowEnd,
                    MassInvMin = massInvMin,
                    MassInvMax = massInvMax,
                    MassIncludeStatic = massIncludeStatic,
                    FactionAllowMask = (uint)factionAllowMask,

                    // ---- WAVE 2: SELECTION ----
                    ThreatStat = new StatSource
                    {
                        Stat = threatStat != null ? threatStat.Key : default,
                        Link = new EntityLinkRef { ReadRootFrom = Target.Self },
                    },
                    ThreatWeightDist = threatWeightDist,
                    ThreatWeightAlign = threatWeightAlign,
                    ThreatWeightStat = threatWeightStat,
                    DwellDescending = dwellDescending,
                    ClosingSpeedFleeing = closingSpeedFleeing,
                    CategoryTiers = categoryTierBlob,

                    // ---- WAVE 2: MULTI ----
                    MaxTargets = math.clamp(maxTargets, 1, 7),
                    WriteHitBuffer = writeHitBuffer,

                    // ---- WAVE 2: VALUE ----
                    ScaledMagnitudeStat = new StatSource
                    {
                        Stat = scaledMagnitudeStat != null ? scaledMagnitudeStat.Key : default,
                        Link = new EntityLinkRef { ReadRootFrom = Target.Self },
                    },
                    MagnitudeBands = magnitudeBandBlob,
                    ApproachBandWidth = approachBandWidth,
                    OverlapThresholdCross = overlapThresholdCross,
                    OverlapThreshold = overlapThreshold,

                    // ---- WAVE 2: ROUTE ----
                    RouteMode = routeMode,
                    MirrorIntoWinner = mirrorIntoWinner,

                    // ---- WAVE 2: STABILITY / BUDGET ----
                    PerTargetRefractoryFrames = (ushort)math.clamp(perTargetRefractoryFrames, 0, ushort.MaxValue),
                    MaxRaycastsPerQuery = math.max(maxRaycastsPerQuery, 0),

                    // ---- WAVE 3: GATING ----
                    DraftCorridorLength = math.max(draftCorridorLength, 0f),
                    DraftCorridorRadius = math.max(draftCorridorRadius, 0f),
                    LedgeRayDepth = math.max(ledgeRayDepth, 0f),
                    PassLaneConeCos = passLaneConeCos,
                    PassLaneRefTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, passLaneRefLink, passLaneRefTarget),
                    ZoneStateInvert = zoneStateInvert,
                    SurfaceMaterialMask = surfaceMaterials.Value,
                    LightExposureThreshold = lightExposureThreshold,
                    LightExposureInvert = lightExposureInvert,

                    // ---- WAVE 3: SELECTION ----
                    HeaviestMassExp = heaviestMassExp,
                    HeaviestSpeedExp = heaviestSpeedExp,
                    BlockingRefTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, blockingRefLink, blockingRefTarget),

                    // ---- WAVE 3: VALUE ----
                    ImpactBands = impactBandBlob,
                    TimingBeatCenter = timingBeatCenter,
                    TimingPerfect = timingPerfect,
                    TimingGreat = timingGreat,
                    TimingGood = timingGood,

                    // ---- WAVE 3: ROUTE ----
                    RedirectLinkKey = redirectKey,

                    // ---- WAVE 3: STABILITY ----
                    DwellToAcquireFrames = (ushort)math.clamp(dwellToAcquireFrames, 0, ushort.MaxValue)
                },
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = ignoreTarget,
                    LinkFilterBlob = filterBlob
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}