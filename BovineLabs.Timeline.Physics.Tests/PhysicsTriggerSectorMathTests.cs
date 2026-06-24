using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    /// <summary>
    /// Unit tests for the DirectionSector quantization helper. Self faces +Z (identity rotation),
    /// up is world +Y, N = 8. Bins: 0=F(+Z), 1=FR, 2=R(+X), 3=BR, 4=B(-Z), 5=BL, 6=L(-X), 7=FL.
    /// </summary>
    public class PhysicsTriggerSectorMathTests
    {
        private static readonly float3 Fwd = new float3(0f, 0f, 1f); // self facing +Z
        private static readonly float3 Up = new float3(0f, 1f, 0f);

        private static int Sector(float3 offset)
        {
            return PhysicsTriggerSectorMath.ComputeSector(offset, Fwd, Up, 8);
        }

        [Test]
        public void BodyAtPlusZ_IsSectorZero_Front()
        {
            Assert.AreEqual(0, Sector(new float3(0f, 0f, 1f)));
        }

        [Test]
        public void BodyAtPlusX_IsSectorTwo_Right()
        {
            Assert.AreEqual(2, Sector(new float3(1f, 0f, 0f)));
        }

        [Test]
        public void BodyAtMinusZ_IsSectorFour_Back()
        {
            Assert.AreEqual(4, Sector(new float3(0f, 0f, -1f)));
        }

        [Test]
        public void BodyAtMinusX_IsSectorSix_Left()
        {
            Assert.AreEqual(6, Sector(new float3(-1f, 0f, 0f)));
        }

        [Test]
        public void Diagonals_FallOnOddBins()
        {
            Assert.AreEqual(1, Sector(new float3(1f, 0f, 1f)), "front-right (+X,+Z) -> FR");
            Assert.AreEqual(3, Sector(new float3(1f, 0f, -1f)), "back-right (+X,-Z) -> BR");
            Assert.AreEqual(5, Sector(new float3(-1f, 0f, -1f)), "back-left (-X,-Z) -> BL");
            Assert.AreEqual(7, Sector(new float3(-1f, 0f, 1f)), "front-left (-X,+Z) -> FL");
        }

        [Test]
        public void Colocated_ReturnsSentinelN_NotFalseFront()
        {
            // |d| below epsilon (also the directly-overhead case once projected onto XZ).
            Assert.AreEqual(8, Sector(float3.zero));
            Assert.AreEqual(8, Sector(new float3(0f, 5f, 0f)), "directly overhead projects to zero in XZ");
        }

        [Test]
        public void HeightComponent_IsIgnored_OnXZPlane()
        {
            // Same XZ bearing as +X, with vertical noise — must still classify as Right.
            Assert.AreEqual(2, Sector(new float3(1f, 3f, 0f)));
        }

        [Test]
        public void Hysteresis_HoldsLastSectorNearBoundary()
        {
            const int n = 8;
            var hyst = PhysicsTriggerSectorMath.DefaultHysteresis(n);

            // A bearing just past the 0/1 boundary (22.5deg) — raw would flip to sector 1,
            // but with lastSector=0 the Schmitt deadband holds it at 0.
            var binW = 2f * math.PI / n;
            var boundary = binW * 0.5f; // 22.5deg, the 0|1 edge
            var justPast = boundary + hyst * 0.5f;

            var offset = new float3(math.sin(justPast), 0f, math.cos(justPast));
            var raw = PhysicsTriggerSectorMath.ComputeRawSector(offset, Fwd, Up, n, out var angle);
            Assert.AreEqual(1, raw, "raw quantization flips past the boundary");

            var stuck = PhysicsTriggerSectorMath.ApplyHysteresis(raw, angle, 0, n, hyst);
            Assert.AreEqual(0, stuck, "hysteresis holds the previous sector inside the deadband");

            // Far past the boundary it must release to the new sector.
            var wellPast = boundary + binW * 0.5f;
            var offset2 = new float3(math.sin(wellPast), 0f, math.cos(wellPast));
            var raw2 = PhysicsTriggerSectorMath.ComputeRawSector(offset2, Fwd, Up, n, out var angle2);
            var released = PhysicsTriggerSectorMath.ApplyHysteresis(raw2, angle2, 0, n, hyst);
            Assert.AreEqual(1, released, "outside the deadband the sector updates");
        }

        [Test]
        public void SectorCountFour_SideSignPreset()
        {
            // N=4: bins 0=F,1=R,2=B,3=L (the SideSign / quadrant preset).
            Assert.AreEqual(0, PhysicsTriggerSectorMath.ComputeSector(new float3(0f, 0f, 1f), Fwd, Up, 4));
            Assert.AreEqual(1, PhysicsTriggerSectorMath.ComputeSector(new float3(1f, 0f, 0f), Fwd, Up, 4));
            Assert.AreEqual(2, PhysicsTriggerSectorMath.ComputeSector(new float3(0f, 0f, -1f), Fwd, Up, 4));
            Assert.AreEqual(3, PhysicsTriggerSectorMath.ComputeSector(new float3(-1f, 0f, 0f), Fwd, Up, 4));
        }

        // ---------------------------------------------------------------------------------------------------
        // WAVE 2 — pure math
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void ClosingSpeed_PositiveWhenOtherMovesTowardSelf()
        {
            // Self at origin, other at +Z 5m moving toward self (-Z). Closing > 0.
            var offset = new float3(0f, 0f, 5f);
            var otherVel = new float3(0f, 0f, -3f);
            var closing = PhysicsTriggerSectorMath.ClosingSpeed(float3.zero, otherVel, offset);
            Assert.Greater(closing, 0f);
            Assert.AreEqual(3f, closing, 1e-4f);
        }

        [Test]
        public void ClosingSpeed_NegativeWhenReceding()
        {
            var offset = new float3(0f, 0f, 5f);
            var otherVel = new float3(0f, 0f, 4f); // moving further away (+Z)
            var closing = PhysicsTriggerSectorMath.ClosingSpeed(float3.zero, otherVel, offset);
            Assert.Less(closing, 0f);
            Assert.AreEqual(-4f, closing, 1e-4f);
        }

        [Test]
        public void ClosingSpeed_UsesRelativeVelocity()
        {
            // Both moving +Z at same speed → no closing.
            var offset = new float3(0f, 0f, 5f);
            var v = new float3(0f, 0f, 2f);
            Assert.AreEqual(0f, PhysicsTriggerSectorMath.ClosingSpeed(v, v, offset), 1e-4f);
        }

        [Test]
        public void ClosingSpeed_ZeroWhenColocated()
        {
            Assert.AreEqual(0f,
                PhysicsTriggerSectorMath.ClosingSpeed(float3.zero, new float3(0f, 0f, 9f), float3.zero), 1e-6f);
        }

        [Test]
        public void FacingSide_BackWhenCandidateLooksAwayFromSelf()
        {
            // Candidate forward = +Z; self is behind it (at -Z relative to candidate).
            var otherFwd = new float3(0f, 0f, 1f);
            var otherToSelf = new float3(0f, 0f, -1f);
            Assert.AreEqual(2, PhysicsTriggerSectorMath.FacingSide(otherFwd, otherToSelf, 0.5f, -0.5f));
        }

        [Test]
        public void FacingSide_FrontWhenCandidateFacesSelf()
        {
            var otherFwd = new float3(0f, 0f, 1f);
            var otherToSelf = new float3(0f, 0f, 1f); // self directly in front of candidate
            Assert.AreEqual(0, PhysicsTriggerSectorMath.FacingSide(otherFwd, otherToSelf, 0.5f, -0.5f));
        }

        [Test]
        public void FacingSide_FlankWhenPerpendicular()
        {
            var otherFwd = new float3(0f, 0f, 1f);
            var otherToSelf = new float3(1f, 0f, 0f); // 90° to the side
            Assert.AreEqual(1, PhysicsTriggerSectorMath.FacingSide(otherFwd, otherToSelf, 0.5f, -0.5f));
        }

        [Test]
        public void VerticalTier_ClassifiesGroundedMidAerial()
        {
            Assert.AreEqual(0, PhysicsTriggerSectorMath.VerticalTier(-2f, -0.5f, 0.5f), "below low → Grounded");
            Assert.AreEqual(1, PhysicsTriggerSectorMath.VerticalTier(0f, -0.5f, 0.5f), "between → Mid");
            Assert.AreEqual(2, PhysicsTriggerSectorMath.VerticalTier(3f, -0.5f, 0.5f), "above high → Aerial");
        }

        [Test]
        public void ApproachVelocityBand_SignsAndBands()
        {
            // band width 2: |closing| < 1 → 0; closing 3 → +2; closing -5 → -3.
            Assert.AreEqual(0, PhysicsTriggerSectorMath.ApproachVelocityBand(0.4f, 2f));
            Assert.AreEqual(2, PhysicsTriggerSectorMath.ApproachVelocityBand(3f, 2f));
            Assert.AreEqual(-3, PhysicsTriggerSectorMath.ApproachVelocityBand(-5f, 2f));
        }

        // ---------------------------------------------------------------------------------------------------
        // WAVE 3 — pure math
        // ---------------------------------------------------------------------------------------------------

        [Test]
        public void NormalSector_QuantizesNormalLikeBearing()
        {
            // A normal pointing +X with self facing +Z, up +Y → sector 2 (Right), same as a +X offset.
            Assert.AreEqual(2, PhysicsTriggerSectorMath.NormalSector(new float3(1f, 0f, 0f), Fwd, Up, 8));
            Assert.AreEqual(0, PhysicsTriggerSectorMath.NormalSector(new float3(0f, 0f, 1f), Fwd, Up, 8));
        }

        [Test]
        public void NormalSector_DegenerateNormal_ReturnsSentinel()
        {
            Assert.AreEqual(8, PhysicsTriggerSectorMath.NormalSector(float3.zero, Fwd, Up, 8));
            // A normal entirely along +Y (up) projects to zero in the XZ plane → sentinel.
            Assert.AreEqual(8, PhysicsTriggerSectorMath.NormalSector(new float3(0f, 1f, 0f), Fwd, Up, 8));
        }

        [Test]
        public void DeflectionSector_ReflectsAcrossNormal()
        {
            // Velocity +Z hitting a wall whose normal is -Z reflects to -Z → sector 4 (Back).
            var v = new float3(0f, 0f, 1f);
            var n = new float3(0f, 0f, -1f);
            Assert.AreEqual(4, PhysicsTriggerSectorMath.DeflectionSector(v, n, Fwd, Up, 8));
        }

        [Test]
        public void DeflectionSector_DegenerateInputs_ReturnSentinel()
        {
            Assert.AreEqual(8,
                PhysicsTriggerSectorMath.DeflectionSector(float3.zero, new float3(0f, 0f, 1f), Fwd, Up, 8));
            Assert.AreEqual(8,
                PhysicsTriggerSectorMath.DeflectionSector(new float3(0f, 0f, 1f), float3.zero, Fwd, Up, 8));
        }

        [Test]
        public void ImpactBand_BucketsImpulseAscending()
        {
            using var builder = new Unity.Entities.BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerDistanceBandBlob>();
            var arr = builder.Allocate(ref root.SquaredThresholds, 3);
            arr[0] = 5f;
            arr[1] = 10f;
            arr[2] = 20f;
            var blob = builder.CreateBlobAssetReference<PhysicsTriggerDistanceBandBlob>(
                Unity.Collections.Allocator.Temp);

            Assert.AreEqual(0, PhysicsTriggerSectorMath.ImpactBand(3f, ref blob.Value.SquaredThresholds), "tap");
            Assert.AreEqual(1, PhysicsTriggerSectorMath.ImpactBand(7f, ref blob.Value.SquaredThresholds));
            Assert.AreEqual(2, PhysicsTriggerSectorMath.ImpactBand(15f, ref blob.Value.SquaredThresholds));
            Assert.AreEqual(3, PhysicsTriggerSectorMath.ImpactBand(99f, ref blob.Value.SquaredThresholds), "slam");
            blob.Dispose();
        }

        [Test]
        public void PerpendicularDistance_RejectsOntoSegment()
        {
            // Segment from origin along +Z; a point at (3,0,5) is 3 units perpendicular off the line.
            var d = PhysicsTriggerSectorMath.PerpendicularDistance(
                new float3(3f, 0f, 5f), float3.zero, new float3(0f, 0f, 1f));
            Assert.AreEqual(3f, d, 1e-4f);

            // A point exactly on the line → 0 perpendicular distance.
            Assert.AreEqual(0f, PhysicsTriggerSectorMath.PerpendicularDistance(
                new float3(0f, 0f, 8f), float3.zero, new float3(0f, 0f, 1f)), 1e-4f);
        }

        [Test]
        public void PerpendicularDistance_DegenerateSegment_FallsBackToRadial()
        {
            var d = PhysicsTriggerSectorMath.PerpendicularDistance(
                new float3(3f, 0f, 4f), float3.zero, float3.zero);
            Assert.AreEqual(5f, d, 1e-4f); // |(3,0,4)| = 5
        }

        [Test]
        public void TimingWindowGrade_GradesByDistanceToBeat()
        {
            // beat at 0.5, windows 0.05 / 0.12 / 0.25.
            Assert.AreEqual(0, PhysicsTriggerSectorMath.TimingWindowGrade(0.5f, 0.5f, 0.05f, 0.12f, 0.25f), "Perfect");
            Assert.AreEqual(1, PhysicsTriggerSectorMath.TimingWindowGrade(0.58f, 0.5f, 0.05f, 0.12f, 0.25f), "Great");
            Assert.AreEqual(2, PhysicsTriggerSectorMath.TimingWindowGrade(0.7f, 0.5f, 0.05f, 0.12f, 0.25f), "Good");
            Assert.AreEqual(3, PhysicsTriggerSectorMath.TimingWindowGrade(0.9f, 0.5f, 0.05f, 0.12f, 0.25f), "Late");
        }

        [Test]
        public void HeaviestMoverScore_MassAndSpeedExponents()
        {
            // a=1,b=0 → pure mass.
            Assert.AreEqual(4f, PhysicsTriggerSectorMath.HeaviestMoverScore(4f, 9f, 1f, 0f), 1e-4f);
            // a=0,b=1 → pure speed.
            Assert.AreEqual(9f, PhysicsTriggerSectorMath.HeaviestMoverScore(4f, 9f, 0f, 1f), 1e-4f);
            // a=1,b=1 → momentum = mass*speed.
            Assert.AreEqual(36f, PhysicsTriggerSectorMath.HeaviestMoverScore(4f, 9f, 1f, 1f), 1e-4f);
            // static (mass 0) with speed term still scores by speed (mass term → 1 when massExp != 0? No: 0).
            Assert.AreEqual(0f, PhysicsTriggerSectorMath.HeaviestMoverScore(0f, 9f, 1f, 1f), 1e-4f,
                "mass 0 with massExp 1 → mass term 0");
            Assert.AreEqual(9f, PhysicsTriggerSectorMath.HeaviestMoverScore(0f, 9f, 0f, 1f), 1e-4f,
                "mass 0 with massExp 0 → mass term 1, pure speed");
        }

        [Test]
        public void DwellToAcquire_CounterIncrementsThenResets()
        {
            ushort dwell = 0;

            // Three consecutive present frames climbs to 3.
            dwell = PhysicsTriggerSectorMath.NextDwell(dwell, true);
            dwell = PhysicsTriggerSectorMath.NextDwell(dwell, true);
            dwell = PhysicsTriggerSectorMath.NextDwell(dwell, true);
            Assert.AreEqual(3, dwell);

            // Required = 3 → acquired now; required = 4 → not yet.
            Assert.IsTrue(PhysicsTriggerSectorMath.DwellAcquired(dwell, 3));
            Assert.IsFalse(PhysicsTriggerSectorMath.DwellAcquired(dwell, 4));

            // An absent frame resets the streak, blocking acquisition again.
            dwell = PhysicsTriggerSectorMath.NextDwell(dwell, false);
            Assert.AreEqual(0, dwell);
            Assert.IsFalse(PhysicsTriggerSectorMath.DwellAcquired(dwell, 3));

            // Required 0 is the no-gate case — always acquired.
            Assert.IsTrue(PhysicsTriggerSectorMath.DwellAcquired(0, 0));
        }

        [Test]
        public void DwellToAcquire_SaturatesAtMax()
        {
            Assert.AreEqual(ushort.MaxValue,
                PhysicsTriggerSectorMath.NextDwell(ushort.MaxValue, true), "dwell saturates, no overflow");
        }
    }
}
