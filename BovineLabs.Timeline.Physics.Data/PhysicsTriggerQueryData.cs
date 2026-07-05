using System;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerQuerySelection : byte
    {
        Nearest,
        Farthest,

        MostAligned,

        LeastAligned,

        // ---- WAVE 2 ----

        /// <summary> score = wDist*proximity + wAlign*alignment + wStat*threatStat (Essence). </summary>
        HighestThreat,

        /// <summary> score = -threatStat (Essence). A missing stat component never wins unless it is alone. </summary>
        WeakestTarget,

        /// <summary> Tiered BelongsTo priority mask, geometric tie-break (champions before minions). </summary>
        CategoryPriority,

        /// <summary> score = ClosingSpeed (fastest incoming, or with sign flip the fastest fleeing). </summary>
        ClosingSpeedSelect,

        /// <summary> Graded line-of-sight openness; cannot early-out so it obeys the raycast budget. </summary>
        MostExposed,

        /// <summary> Per-candidate dwell-counter score (FreshestContact = ASC, DwellSelect = DESC). </summary>
        DwellSelect,

        /// <summary> Fire one event per gated survivor with its OWN value (the AoE boom). </summary>
        AllSurvivorsFanout,

        /// <summary> Top-K by score routed into indexed slots, value = rank (single-winner = rank 0). </summary>
        TopK,

        // ---- WAVE 3 ----

        /// <summary> score = mass^a * |v|^b (HeaviestMover / MostMomentum). Shares PhysicsMass + PhysicsVelocity. </summary>
        HeaviestMover,

        /// <summary> score = -perpendicular distance to the self→referenceTarget segment (MostBlocking / RacingLineThreat). </summary>
        MostBlocking,

        /// <summary> A candidate carrying an unexpired TriggerQueryTaunt instantly wins, locked (external taunt binding). </summary>
        TauntOverride,

        /// <summary> Order survivors by a stable key (angle then entity index) and pick the successor of LastWinner. </summary>
        TabCycle
    }

    /// <summary> How the int payload carried by the fired ConditionEvent is computed from the winner. </summary>
    public enum PhysicsTriggerQueryValueMode : byte
    {
        /// <summary> Emit the authored constant <see cref="PhysicsTriggerQueryData.FoundValue"/> (legacy behaviour). </summary>
        Constant,

        /// <summary> Quantize the bearing of the winner relative to self into one of N sectors (the 8-directional map). </summary>
        DirectionSector,

        /// <summary> Bucket the squared distance against ascending squared thresholds → band index. </summary>
        DistanceBand,

        /// <summary> band * sectorCount + sector — one int encodes a 2D polar cell. </summary>
        SectorBandPacked,

        // ---- WAVE 2 ----

        /// <summary> Clamped count of gated survivors (optionally only the threshold-cross via prevCount). </summary>
        OverlapCount,

        /// <summary> Front / Flank / Back of the winner measured in the CANDIDATE's own frame. </summary>
        FacingSide,

        /// <summary> Map the winner's BelongsTo bitmask to an ordinal via the authored tier list. </summary>
        CategoryOrdinal,

        /// <summary> Sign / band of ClosingSpeed (whiff-punish vs guard-break). </summary>
        ApproachVelocityBand,

        /// <summary> Quantize an Essence stat scalar into an authored band table → int. </summary>
        ScaledMagnitude,

        // ---- WAVE 3 ----

        /// <summary> Quantize the StatefulCollisionEvent.Normal into a sector/face int (which face was struck).
        /// No collision event that frame → sentinel == SectorCount. </summary>
        ContactNormalSector,

        /// <summary> Bucket the collision EstimatedImpulse into ascending bands (tap cracks, slam shatters).
        /// No collision details that frame → band 0. </summary>
        ImpactBand,

        /// <summary> On Enter, clip-local normalized time vs an authored beat centre → Perfect/Great/Good/Late ordinal. </summary>
        TimingWindowGrade,

        /// <summary> The shared LoS ray result as the value: 0 = visible (clear), 1 = hidden (occluded). </summary>
        OcclusionState,

        /// <summary> Reflect self-velocity across the collision normal, quantized into sector bins.
        /// No collision normal that frame → sentinel == SectorCount. </summary>
        DeflectionBounce,

        /// <summary> MULTI only: emit the DirectionSector of the survivor centroid (Σpos/count) as every survivor's
        /// value — the vortex that pulls into the swarm centre. Single-winner falls back to Constant. </summary>
        AggregateCentroid,

        /// <summary> Bin the winner onto a GridCols×GridRows CARTESIAN grid → cell index (row-major, top-left = 0).
        /// SectorPlane picks the plane: XZ = ground grid (rows = near/far), else = frontal wall (rows = height).
        /// Out-of-bounds hits clamp to the edge cells. The cartesian cousin of DirectionSector. </summary>
        PlanarGrid
    }

    /// <summary> Gating modes applied (AND'd) before selection. A bit mask so multiple gates chain. </summary>
    [Flags]
    public enum PhysicsTriggerGateFlags : uint
    {
        None = 0,

        /// <summary> Keep only candidates whose ClosingSpeed &gt; MinClosingSpeed (or, inverted, only receding). </summary>
        ApproachGate = 1 << 0,

        /// <summary> Keep only candidates whose back is to self (or, inverted, face-to-face). </summary>
        FacingGate = 1 << 1,

        /// <summary> Classify offset.y into a height tier and keep only the allowed tiers. </summary>
        VerticalGate = 1 << 2,

        /// <summary> Gate acquisition to a clip-normalized active sub-range. </summary>
        FrameWindowGate = 1 << 3,

        /// <summary> Keep only candidates whose PhysicsMass.InverseMass falls in the allowed bracket. </summary>
        MassBracket = 1 << 4,

        /// <summary> Invert the line-of-sight test: keep only OCCLUDED candidates (flush-them-out). </summary>
        RequireOccluded = 1 << 5,

        /// <summary> Keep only candidates whose FactionMember.Faction is in FactionAllowMask. </summary>
        FactionGate = 1 << 6,

        // ---- WAVE 3 ----

        /// <summary> DraftCorridorGate: keep only candidates in the rear slipstream capsule behind a leader. </summary>
        DraftCorridorGate = 1 << 7,

        /// <summary> LedgeGate: short down-ray from the candidate; exclude if it hits ground (counts the ray budget). </summary>
        LedgeGate = 1 << 8,

        /// <summary> PassLaneCone: a second cone whose axis is (refPos - selfPos); keep only candidates inside it. </summary>
        PassLaneCone = 1 << 9,

        /// <summary> ZoneStateGate: require (or, inverted, exclude) the enableable TriggerQueryZoneTag on the candidate. </summary>
        ZoneStateGate = 1 << 10,

        /// <summary> SurfaceMaterialGate: gate by the collision contact material/tag (no-op without a collision event). </summary>
        SurfaceMaterialGate = 1 << 11,

        /// <summary> LightExposureGate: gate by an external TriggerQueryExposure.Value vs a threshold. </summary>
        LightExposureGate = 1 << 12
    }

    /// <summary> Which routed-role entities the ExcludeRoles gate skips. </summary>
    [Flags]
    public enum PhysicsTriggerRoleMask : byte
    {
        None = 0,
        Owner = 1 << 0,
        Source = 1 << 1,
        Target = 1 << 2,
        Self = 1 << 3
    }

    /// <summary> How the route destination slot/link is chosen. </summary>
    public enum PhysicsTriggerRouteMode : byte
    {
        /// <summary> Always the authored RouteSlot / RouteTo (legacy). </summary>
        Fixed,

        /// <summary> Destination slot chosen by the computed Value (RouteByValue / RouteBySector). </summary>
        ByValue,

        // ---- WAVE 3 ----

        /// <summary> RedirectToLinkedRole: route into the slot named by the WINNER's outbound EntityLink (pet → master).
        /// Cross-entity resolution; the write stays single-threaded in the ApplyJob. </summary>
        LinkedRole
    }

    /// <summary> Basis the <see cref="PhysicsTriggerQueryValueMode.DirectionSector"/> bearing is measured against. </summary>
    public enum PhysicsTriggerSectorReference : byte
    {
        /// <summary> Bearing is relative to the bound entity's forward (sector 0 = dead ahead). </summary>
        SelfForward,

        /// <summary> Bearing is relative to world +Z (still half-bin biased so sector 0 centres on +Z). </summary>
        World
    }

    /// <summary> Projection plane used to flatten the offset before measuring the bearing. </summary>
    public enum PhysicsTriggerSectorPlane : byte
    {
        /// <summary> Project onto the world XZ plane (default). </summary>
        XZ,

        /// <summary> Project onto the plane perpendicular to the self up axis (view-relative). </summary>
        ViewRelative,

        /// <summary> Project onto the plane perpendicular to <see cref="PhysicsTriggerQueryData.SectorCustomUp"/>. </summary>
        CustomAxis
    }

    /// <summary> Which Targets slot the routed winner is written into. </summary>
    /// <remarks> Custom is the zero value so default-constructed data reproduces the legacy Custom write. </remarks>
    public enum PhysicsTriggerRouteSlot : byte
    {
        Custom,
        Owner,
        Source,
        Target
    }

    /// <summary> How the routed slot is written. </summary>
    public enum PhysicsTriggerWriteMode : byte
    {
        /// <summary> Always overwrite the slot with the winner (legacy behaviour). </summary>
        Set,

        /// <summary> Only ever clear the slot on loss; never write a winner. </summary>
        ClearOnly,

        /// <summary> Only write the winner if the slot is currently Entity.Null — never clobber an authored target. </summary>
        SetIfEmpty
    }

    public struct PhysicsTriggerQueryData : IComponentData
    {
        public StatefulEventState EventState;

        public uint CollidesWithMask;

        public float MaxDistance;

        public float MaxAngle;

        public bool RequireLineOfSight;
        public uint ObstacleMask;
        public float LineOfSightOffset;

        public PhysicsTriggerQuerySelection Selection;

        public EntityLinkRef RouteTo;

        public bool ClearOnLost;

        public ConditionKey FoundCondition;
        public int FoundValue;
        public ConditionKey LostCondition;
        public int LostValue;

        // ---- WAVE 1: VALUE ----
        public PhysicsTriggerQueryValueMode ValueMode;
        public int SectorCount; // 4 / 8 / 16
        public PhysicsTriggerSectorReference SectorReference;
        public PhysicsTriggerSectorPlane SectorPlane;
        public float3 SectorCustomUp;
        public float SectorHysteresis; // radians
        public BlobAssetReference<PhysicsTriggerDistanceBandBlob> DistanceBands; // ascending SQUARED thresholds

        // PlanarGrid (cartesian tiles). Plane/basis reuse SectorPlane (XZ = ground, else frontal wall) + SectorReference.
        public int GridCols; // >= 1 (3 for a 3×3)
        public int GridRows; // >= 1
        public float GridHalfWidth; // half the grid width along self-right (m)
        public float GridHalfHeight; // half the grid height along the vertical axis (m)
        public float GridHysteresis; // Schmitt deadband as a fraction of ONE cell (<= 0 = off)

        // ---- WAVE 1: ROUTE ----
        public PhysicsTriggerRouteSlot RouteSlot;
        public PhysicsTriggerWriteMode WriteMode;

        // ---- WAVE 1: STABILITY ----
        public int SwitchMargin; // ×100; StickyWinner displacement threshold
        public ushort GraceFrames; // LostDebounce hold frames

        // ---- WAVE 2: GATING ----
        public PhysicsTriggerRoleMask ExcludeRoles; // {Owner,Source,Target,Self} candidates to skip
        public PhysicsTriggerGateFlags Gates; // chainable gate mask
        public float MinClosingSpeed; // ApproachGate threshold (m/s)
        public bool ApproachRecedingOnly; // ApproachGate polarity: keep receding instead of closing
        public float FacingCosThreshold; // FacingGate: dot(otherFwd, dirToSelf) > this
        public bool FacingFaceToFace; // FacingGate polarity: require facing self instead of back-turned
        public float VerticalMidLow; // VerticalGate: offset.y < this == Grounded
        public float VerticalMidHigh; // VerticalGate: offset.y > this == Aerial
        public byte VerticalTierMask; // bit0 Grounded, bit1 Mid, bit2 Aerial — allowed tiers
        public float FrameWindowStart; // FrameWindowGate: clip-normalized [0,1] active sub-range
        public float FrameWindowEnd;
        public float MassInvMin; // MassBracket: inclusive InverseMass bracket
        public float MassInvMax;
        public bool MassIncludeStatic; // MassBracket: treat missing PhysicsMass (static) as InverseMass 0
        public uint FactionAllowMask; // FactionGate: allowed (1 << faction) bits

        // ---- WAVE 2: SELECTION ----
        public StatSource ThreatStat; // HighestThreat / WeakestTarget Essence stat read
        public float ThreatWeightDist; // HighestThreat: proximity weight
        public float ThreatWeightAlign; // HighestThreat: alignment weight
        public float ThreatWeightStat; // HighestThreat: stat weight
        public bool DwellDescending; // DwellSelect: false = freshest (ASC), true = longest-committed (DESC)
        public bool ClosingSpeedFleeing; // ClosingSpeedSelect: pick fastest fleeing instead of fastest incoming
        public BlobAssetReference<PhysicsTriggerCategoryTierBlob> CategoryTiers; // CategoryPriority / CategoryOrdinal

        // ---- WAVE 2: MULTI ----
        public int MaxTargets; // AllSurvivorsFanout / TopK hard cap (<= 7). 0 → 1 (single-winner)
        public bool WriteHitBuffer; // also emit a capped DynamicBuffer<TriggerQueryHit>

        // ---- WAVE 2: VALUE ----
        public StatSource ScaledMagnitudeStat; // ScaledMagnitude Essence stat read
        public BlobAssetReference<PhysicsTriggerDistanceBandBlob> MagnitudeBands; // ScaledMagnitude quantize table
        public float ApproachBandWidth; // ApproachVelocityBand: m/s per band step
        public bool OverlapThresholdCross; // OverlapCount: emit only on threshold cross
        public int OverlapThreshold;

        // ---- WAVE 2: ROUTE ----
        public PhysicsTriggerRouteMode RouteMode; // Fixed / ByValue
        public bool MirrorIntoWinner; // also write self into the winner's slot (single-threaded ApplyJob)

        // ---- WAVE 2: STABILITY ----
        public ushort PerTargetRefractoryFrames; // PerTargetRefractory: a winner can't re-win for this many frames

        // ---- WAVE 2: PERFORMANCE ----
        public int MaxRaycastsPerQuery; // LoS family budget. 0 → unlimited (legacy)

        // ---- WAVE 3: GATING ----
        public float DraftCorridorLength; // DraftCorridorGate: how far behind the leader the slipstream reaches (m)
        public float DraftCorridorRadius; // DraftCorridorGate: max perpendicular distance from the leader's back line
        public float LedgeRayDepth; // LedgeGate: down-ray length; a hit within this depth = ground (excluded)
        public float PassLaneConeCos; // PassLaneCone: cos half-angle of the second cone toward the reference target
        public EntityLinkRef PassLaneRefTarget; // PassLaneCone: which Targets slot / role is the reference the cone points at, + optional EntityLink
        public bool ZoneStateInvert; // ZoneStateGate: false = require the tag enabled, true = exclude if enabled
        public uint SurfaceMaterialMask; // SurfaceMaterialGate: allowed contact-material/custom-tag bits (collision filter)
        public float LightExposureThreshold; // LightExposureGate: keep candidates with Exposure.Value within the band
        public bool LightExposureInvert; // LightExposureGate: false = keep brighter than threshold, true = keep darker

        // ---- WAVE 3: SELECTION ----
        public float HeaviestMassExp; // HeaviestMover: mass exponent a (1 = mass term linear)
        public float HeaviestSpeedExp; // HeaviestMover: speed exponent b (0 = ignore speed → pure mass)
        public EntityLinkRef BlockingRefTarget; // MostBlocking: the reference endpoint of the self→ref segment, + optional EntityLink

        // ---- WAVE 3: VALUE ----
        public BlobAssetReference<PhysicsTriggerDistanceBandBlob> ImpactBands; // ImpactBand: ascending impulse thresholds
        public float TimingBeatCenter; // TimingWindowGrade: authored beat centre in clip-normalized [0,1]
        public float TimingPerfect; // TimingWindowGrade: |t-beat| <= this → Perfect (0)
        public float TimingGreat; // TimingWindowGrade: <= this → Great (1)
        public float TimingGood; // TimingWindowGrade: <= this → Good (2); else Late (3)

        // ---- WAVE 3: ROUTE ----
        public ushort RedirectLinkKey; // RedirectToLinkedRole: the winner's outbound EntityLink key (pet → master)

        // ---- WAVE 3: STABILITY ----
        public ushort DwellToAcquireFrames; // require a candidate to survive gating N consecutive frames before it wins
    }

    /// <summary> Tier list mapping a BelongsTo bitmask to a priority ordinal (higher = stronger / earlier). </summary>
    public struct PhysicsTriggerCategoryTierBlob
    {
        /// <summary> Parallel to <see cref="Ordinals"/>: the BelongsTo bitmask matched against the candidate. </summary>
        public BlobArray<uint> Masks;

        /// <summary> The ordinal assigned when the candidate's BelongsTo intersects the matching mask. </summary>
        public BlobArray<int> Ordinals;
    }

    /// <summary>
    /// Ascending band thresholds, pre-transformed at bake to match their comparand: SQUARED for the distance-band
    /// table (compared against distSq), RAW for the magnitude/impact tables. The field is named neutrally because
    /// the same blob type backs all three — do not assume the values are squared. See PhysicsTriggerBakingUtility.
    /// </summary>
    public struct PhysicsTriggerDistanceBandBlob
    {
        public BlobArray<float> Thresholds;
    }

    public struct PhysicsTriggerQueryState : IComponentData
    {
        // selection / lost debounce (StickyWinner, LostDebounce)
        public Entity LastWinner;
        public int LastScore; // ×100 fixed-point
        public ushort DwellFrames;
        public ushort GraceCountdown;
        public float3 LastKnownPos;

        // value hysteresis (DirectionSector Schmitt)
        public sbyte LastSector; // -1 = none

        // ---- WAVE 2 ----

        // count / threshold (OverlapCount cross)
        public int PrevCount;

        // multi-winner found/lost edges (AllSurvivorsFanout, TopK) — cap = MaxTargets
        public FixedList64Bytes<Entity> LastWinnerSet;

        // per-candidate persistent state (DwellSelect / PerTargetRefractory) — cap MaxTrackedCandidates
        public FixedList128Bytes<EntityCounter> Tracked;
    }

    /// <summary> Per-candidate cross-frame counter: dwell frames and a refractory countdown. </summary>
    public struct EntityCounter
    {
        public Entity Entity;
        public ushort Dwell; // consecutive gated frames this body has been present
        public ushort Refractory; // framesUntilEligible; > 0 means this body can't win
        public byte Seen; // 1 if touched this frame (for prune)
    }

    /// <summary> One survivor of a multi-winner fanout, optionally written to a capped DynamicBuffer. </summary>
    [InternalBufferCapacity(8)]
    public struct TriggerQueryHit : IBufferElementData
    {
        public Entity Entity;
        public int Sector;
        public int Band;
        public int Score; // ×100 fixed-point
    }

    /// <summary>
    /// Generic team/faction tag the FactionGate reads. This package only defines the MECHANISM; games bind
    /// the value (assign Faction on their bodies) and author the FactionAllowMask. Not an Essence-specific type.
    /// </summary>
    public struct FactionMember : IComponentData
    {
        public int Faction;
    }

    /// <summary>
    /// Generic enableable tag the ZoneStateGate reads (Frozen / OnFire / Wet / InZone — game-defined meaning).
    /// This package only defines the MECHANISM; games enable/disable it on their bodies. TODO external zone binding.
    /// </summary>
    public struct TriggerQueryZoneTag : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// Generic external-illumination value the LightExposureGate reads. Games drive Value from their light system.
    /// TODO external light system binding — this package only defines the field a downstream system writes.
    /// </summary>
    public struct TriggerQueryExposure : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Generic taunt marker the TauntOverride selection reads: a candidate whose UntilTime is still in the future
    /// instantly wins, locked. Games set UntilTime to the world time the taunt expires. TODO game taunt binding.
    /// </summary>
    public struct TriggerQueryTaunt : IComponentData
    {
        public float UntilTime;
    }
}
