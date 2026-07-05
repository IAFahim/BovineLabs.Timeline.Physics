using NUnit.Framework;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Tests
{
    /// <summary>
    /// Unit tests for the PlanarGrid cell quantization. Frontal wall: self faces +Z, up = +Y, so the grid's
    /// horizontal axis is self-right (+X) and its vertical axis is world up (+Y). 3×3, half extents 1.5m.
    /// Cells are row-major, top-left = 0:  0 1 2 / 3 4 5 / 6 7 8.
    /// </summary>
    public class PhysicsTriggerGridMathTests
    {
        private static readonly float3 Fwd = new float3(0f, 0f, 1f);
        private static readonly float3 Up = new float3(0f, 1f, 0f);

        private static int Cell(float x, float y)
        {
            // frontal wall (ground = false), 3×3, ±1.5m each axis
            return PhysicsTriggerGridMath.ComputeCell(new float3(x, y, 0f), Fwd, Up, false, 1.5f, 1.5f, 3, 3,
                out _, out _);
        }

        [Test]
        public void Center_IsCellFour()
        {
            Assert.AreEqual(4, Cell(0f, 0f));
        }

        [Test]
        public void TopLeft_IsCellZero()
        {
            Assert.AreEqual(0, Cell(-1f, 1f)); // left (−X), high (+Y)
        }

        [Test]
        public void TopCenter_IsCellOne()
        {
            Assert.AreEqual(1, Cell(0f, 1f)); // "hit from the top" = 0,1,2
        }

        [Test]
        public void TopRight_IsCellTwo()
        {
            Assert.AreEqual(2, Cell(1f, 1f));
        }

        [Test]
        public void BottomRow_IsSixSevenEight()
        {
            Assert.AreEqual(6, Cell(-1f, -1f));
            Assert.AreEqual(7, Cell(0f, -1f));
            Assert.AreEqual(8, Cell(1f, -1f));
        }

        [Test]
        public void OutOfBounds_ClampsToEdgeCell()
        {
            Assert.AreEqual(2, Cell(99f, 99f)); // far top-right → clamps into corner cell, never out of range
            Assert.AreEqual(6, Cell(-99f, -99f));
        }

        [Test]
        public void GroundGrid_UsesForwardAsVerticalAxis()
        {
            // ground = true: vertical axis is fwd (+Z). Far-forward (+Z) is the "top" row → cell 1 (top-centre).
            var far = PhysicsTriggerGridMath.ComputeCell(new float3(0f, 0f, 1f), Fwd, Up, true, 1.5f, 1.5f, 3, 3,
                out _, out _);
            var near = PhysicsTriggerGridMath.ComputeCell(new float3(0f, 0f, -1f), Fwd, Up, true, 1.5f, 1.5f, 3, 3,
                out _, out _);
            Assert.AreEqual(1, far);  // far = top row
            Assert.AreEqual(7, near); // near = bottom row
        }

        [Test]
        public void Hysteresis_KeepsLastCellNearBoundary()
        {
            // A point just past the col 0/1 boundary, with last cell = 3 (mid-left), stays sticky within the margin.
            PhysicsTriggerGridMath.ComputeCell(new float3(0.02f, 0f, 0f), Fwd, Up, false, 1.5f, 1.5f, 3, 3,
                out var u, out var v);
            var raw = PhysicsTriggerGridMath.ComputeCell(new float3(0.02f, 0f, 0f), Fwd, Up, false, 1.5f, 1.5f, 3, 3,
                out _, out _);
            var stuck = PhysicsTriggerGridMath.ApplyHysteresis(raw, u, v, 3, 3, 3, 0.15f);
            Assert.AreEqual(3, stuck); // held to the previous cell, no chatter
        }

        [Test]
        public void Hysteresis_ReleasesWhenFarFromLastCell()
        {
            var raw = PhysicsTriggerGridMath.ComputeCell(new float3(1.2f, 1.2f, 0f), Fwd, Up, false, 1.5f, 1.5f, 3, 3,
                out var u, out var v);
            var cell = PhysicsTriggerGridMath.ApplyHysteresis(raw, u, v, 6, 3, 3, 0.15f);
            Assert.AreEqual(2, cell); // clearly in the top-right cell now, last cell 6 doesn't hold it
        }
    }
}
