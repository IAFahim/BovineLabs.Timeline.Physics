using Unity.Burst;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Burst-free-callable static helpers for the <see cref="PhysicsTriggerQueryValueMode.PlanarGrid"/> VALUE mode:
    /// bin the winner's position onto a Cols×Rows cartesian grid and emit the cell index. The cartesian cousin of
    /// <see cref="PhysicsTriggerSectorMath.ComputeSector"/> (which is polar). Pure functions → unit-testable without ECS.
    ///
    /// Cell convention: ROW-MAJOR, TOP-LEFT = 0 (reading order). For a 3×3 the top row is 0,1,2 and the bottom row is
    /// 6,7,8. "Top" means higher on the vertical axis (world up for a frontal wall, farther-forward for a ground grid).
    /// Out-of-bounds hits CLAMP to the edge cells — every hit lands in exactly one cell (no sentinel).
    /// </summary>
    [BurstCompile]
    public static class PhysicsTriggerGridMath
    {
        /// <summary>
        /// Bin <paramref name="offset"/> (winnerPos − selfPos, world space) into a Cols×Rows grid.
        /// The grid's horizontal axis is self-right = cross(up, fwd); the vertical axis is <paramref name="up"/> for a
        /// frontal wall, or <paramref name="fwd"/> for a ground grid (<paramref name="ground"/> = true). Extents are
        /// HALF sizes in metres, so the grid spans [−halfWidth, +halfWidth] × [−halfHeight, +halfHeight].
        /// </summary>
        /// <param name="offset">winnerPos − selfPos (world space); or the contact point − selfPos.</param>
        /// <param name="fwd">World-space forward basis (rotate(selfRot,+Z) or world +Z) — the wall normal / grid depth axis.</param>
        /// <param name="up">World-space up basis for the plane (world +Y, view up, or a custom axis).</param>
        /// <param name="ground">true → ground grid: vertical axis is fwd (near/far). false → frontal wall: vertical axis is up.</param>
        /// <param name="halfWidth">Half the grid width along self-right (metres).</param>
        /// <param name="halfHeight">Half the grid height along the vertical axis (metres).</param>
        /// <param name="cols">Column count (&gt;= 1).</param>
        /// <param name="rows">Row count (&gt;= 1).</param>
        /// <param name="u">Out: normalized horizontal position, clamped to [0,1] (0 = left edge).</param>
        /// <param name="v">Out: normalized vertical position, clamped to [0,1] (0 = bottom edge).</param>
        /// <returns>Cell index 0 .. cols*rows-1, row-major, top-left = 0.</returns>
        public static int ComputeCell(float3 offset, float3 fwd, float3 up, bool ground, float halfWidth,
            float halfHeight, int cols, int rows, out float u, out float v)
        {
            cols = math.max(cols, 1);
            rows = math.max(rows, 1);

            up = math.normalizesafe(up, new float3(0f, 1f, 0f));
            var fwdN = math.normalizesafe(fwd, new float3(0f, 0f, 1f));

            // right = cross(up, fwd): self-right (+X when fwd=+Z, up=+Y) — the same LEFT-handed basis the sector math uses.
            var right = math.normalizesafe(math.cross(up, fwdN), new float3(1f, 0f, 0f));
            var vertAxis = ground ? fwdN : up;

            var hw = math.max(halfWidth, PhysicsTriggerSectorMath.Epsilon);
            var hh = math.max(halfHeight, PhysicsTriggerSectorMath.Epsilon);

            // Project onto (right, vertAxis) and remap [-half,+half] → [0,1].
            u = math.saturate(math.dot(offset, right) / hw * 0.5f + 0.5f);
            v = math.saturate(math.dot(offset, vertAxis) / hh * 0.5f + 0.5f);

            var col = math.clamp((int)math.floor(u * cols), 0, cols - 1);
            var rowFromBottom = math.clamp((int)math.floor(v * rows), 0, rows - 1);
            var rowFromTop = rows - 1 - rowFromBottom; // top-left = 0

            return rowFromTop * cols + col;
        }

        /// <summary>
        /// Linear Schmitt hysteresis: if the normalized point (<paramref name="u"/>,<paramref name="v"/>) is still
        /// within <paramref name="margin"/> (a fraction of ONE cell) of the last cell's span on BOTH axes, keep the
        /// last cell to suppress boundary chatter. Mirrors <see cref="PhysicsTriggerSectorMath.ApplyHysteresis"/> for
        /// the cartesian case. Pass the result back as lastCell next frame; -1 = no previous cell.
        /// </summary>
        public static int ApplyHysteresis(int rawCell, float u, float v, int lastCell, int cols, int rows, float margin)
        {
            cols = math.max(cols, 1);
            rows = math.max(rows, 1);
            var count = cols * rows;

            if (margin <= 0f || lastCell < 0 || lastCell >= count || lastCell == rawCell)
            {
                return rawCell;
            }

            var lastCol = lastCell % cols;
            var lastRowFromTop = lastCell / cols;
            var lastRowFromBottom = rows - 1 - lastRowFromTop;

            var mu = margin / cols; // margin is a fraction of one cell → normalize per axis
            var mv = margin / rows;

            var uLo = (float)lastCol / cols - mu;
            var uHi = (float)(lastCol + 1) / cols + mu;
            var vLo = (float)lastRowFromBottom / rows - mv;
            var vHi = (float)(lastRowFromBottom + 1) / rows + mv;

            if (u >= uLo && u <= uHi && v >= vLo && v <= vHi)
            {
                return lastCell; // still inside the sticky band — no chatter
            }

            return rawCell;
        }
    }
}
