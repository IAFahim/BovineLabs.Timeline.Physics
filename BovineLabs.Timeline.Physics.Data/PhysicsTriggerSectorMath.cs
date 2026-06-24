using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    /// <summary>
    /// Burst-free-callable static helpers for the DirectionSector / DistanceBand VALUE modes.
    /// Pure functions so the sector quantization is unit-testable without the full ECS system.
    /// </summary>
    [BurstCompile]
    public static class PhysicsTriggerSectorMath
    {
        public const float Epsilon = 1e-6f;

        /// <summary>
        /// Quantize the planar bearing of <paramref name="offset"/> relative to a forward basis into one of
        /// <paramref name="sectorCount"/> sectors. Unity is LEFT-handed; the basis is explicit, not hand-waved.
        /// Bins: 0=F, 1=FR, 2=R, 3=BR, 4=B, 5=BL, 6=L, 7=FL (at N=8). +half-bin centres forward on bin 0.
        /// Degenerate (|d| &lt; eps) → sentinel == sectorCount (never a false "front").
        /// </summary>
        /// <param name="offset">winnerPos - selfPos (world space).</param>
        /// <param name="fwd">World-space forward basis (e.g. rotate(selfRot, +Z), or world +Z).</param>
        /// <param name="up">Plane up axis the offset/forward are projected against.</param>
        /// <param name="sectorCount">Number of bins (4 / 8 / 16).</param>
        /// <returns>Sector 0..N-1, or N for the degenerate / co-located case.</returns>
        public static int ComputeSector(float3 offset, float3 fwd, float3 up, int sectorCount)
        {
            return ComputeRawSector(offset, fwd, up, sectorCount, out _);
        }

        /// <summary>
        /// As <see cref="ComputeSector"/> but also returns the continuous bearing angle <paramref name="angle"/>
        /// in [0, 2π) used for the Schmitt value-hysteresis. Returns sentinel N (and angle = NaN) when degenerate.
        /// </summary>
        public static int ComputeRawSector(float3 offset, float3 fwd, float3 up, int sectorCount, out float angle)
        {
            angle = float.NaN;
            if (sectorCount < 1)
            {
                return 0;
            }

            up = math.normalizesafe(up, new float3(0f, 1f, 0f));

            // Project forward + offset onto the plane perpendicular to up.
            var fwdPlanar = fwd - up * math.dot(fwd, up);
            var fwdN = math.normalizesafe(fwdPlanar, new float3(0f, 0f, 1f));

            // right = cross(up, fwd) — in LH this points to self-right (+X when fwd=+Z, up=+Y).
            var right = math.cross(up, fwdN);

            var d = offset - up * math.dot(offset, up);
            if (math.lengthsq(d) < Epsilon * Epsilon)
            {
                return sectorCount; // sentinel = N, NOT 0
            }

            var a = math.atan2(math.dot(d, right), math.dot(d, fwdN)); // (-pi, pi], 0 = dead ahead
            if (a < 0f)
            {
                a += 2f * math.PI; // [0, 2pi)
            }

            angle = a;

            var binW = 2f * math.PI / sectorCount; // 45deg at N=8
            var raw = (int)math.floor((a + binW * 0.5f) / binW); // +half-bin CENTERS forward on bin 0
            return ((raw % sectorCount) + sectorCount) % sectorCount; // safe for any N
        }

        /// <summary> Wrap an angle into (-π, π]. </summary>
        public static float AngleWrap(float a)
        {
            a = math.fmod(a, 2f * math.PI);
            if (a > math.PI)
            {
                a -= 2f * math.PI;
            }
            else if (a <= -math.PI)
            {
                a += 2f * math.PI;
            }

            return a;
        }

        /// <summary>
        /// Apply Schmitt value-hysteresis: if the new bearing is still within (half-bin + hyst) of the centre of
        /// <paramref name="lastSector"/>, keep the last sector to suppress boundary chatter.
        /// </summary>
        /// <returns>The hysteresis-stabilised sector. Pass the result back as lastSector next frame.</returns>
        public static int ApplyHysteresis(int rawSector, float angle, int lastSector, int sectorCount, float hyst)
        {
            if (rawSector == sectorCount)
            {
                return rawSector; // degenerate — don't touch lastSector here
            }

            if (lastSector >= 0 && lastSector < sectorCount && !float.IsNaN(angle))
            {
                var binW = 2f * math.PI / sectorCount;
                var centerOfLast = lastSector * binW;
                if (math.abs(AngleWrap(angle - centerOfLast)) < binW * 0.5f + hyst)
                {
                    return lastSector; // sticky -> no boundary chatter
                }
            }

            return rawSector;
        }

        /// <summary> The default Schmitt deadband: ~0.15 of one bin width. </summary>
        public static float DefaultHysteresis(int sectorCount)
        {
            return 0.15f * (2f * math.PI / sectorCount);
        }

        // ---------------------------------------------------------------------------------------------------
        // WAVE 2 — shared pure math (ClosingSpeed, FacingSide, VerticalTier, band quantization, ordinal map).
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Signed approach speed of <paramref name="otherVel"/> toward self along the line from self to other.
        /// Positive = closing (the other body is moving toward self), negative = receding.
        /// </summary>
        /// <param name="selfVel">Self linear velocity (world).</param>
        /// <param name="otherVel">Candidate linear velocity (world). Static / sleeping → pass float3.zero.</param>
        /// <param name="offset">otherPos - selfPos (world). Degenerate (co-located) → 0.</param>
        public static float ClosingSpeed(float3 selfVel, float3 otherVel, float3 offset)
        {
            var distSq = math.lengthsq(offset);
            if (distSq < Epsilon * Epsilon)
            {
                return 0f;
            }

            // dir points from other toward self; a body moving along it (relative to self) is closing.
            var dir = -offset * math.rsqrt(distSq);
            return math.dot(otherVel - selfVel, dir);
        }

        /// <summary>
        /// Classify which face of the CANDIDATE self is on, in the candidate's own frame.
        /// 0 = Front (self is in front of the candidate), 1 = Flank, 2 = Back (self is behind the candidate).
        /// Degenerate offset → Front (0).
        /// </summary>
        /// <param name="otherFwd">Candidate world-space forward (rotate(otherRot, +Z)).</param>
        /// <param name="otherToSelf">selfPos - otherPos (world).</param>
        /// <param name="frontCos">cos of the front half-cone (e.g. cos(60°)); above → Front.</param>
        /// <param name="backCos">cos of the back half-cone, negative (e.g. cos(120°)); below → Back.</param>
        public static int FacingSide(float3 otherFwd, float3 otherToSelf, float frontCos, float backCos)
        {
            var lenSq = math.lengthsq(otherToSelf);
            if (lenSq < Epsilon * Epsilon)
            {
                return 0;
            }

            var d = math.dot(math.normalizesafe(otherFwd, new float3(0f, 0f, 1f)),
                otherToSelf * math.rsqrt(lenSq));

            if (d >= frontCos)
            {
                return 0; // self is in front of the candidate
            }

            if (d <= backCos)
            {
                return 2; // self is behind the candidate
            }

            return 1; // flank
        }

        /// <summary>
        /// Classify a vertical offset into a tier: 0 = Grounded (y &lt; low), 1 = Mid, 2 = Aerial (y &gt; high).
        /// </summary>
        public static int VerticalTier(float offsetY, float midLow, float midHigh)
        {
            if (offsetY < midLow)
            {
                return 0;
            }

            return offsetY > midHigh ? 2 : 1;
        }

        /// <summary>
        /// Bucket <paramref name="value"/> against ascending thresholds (already squared if you pass squared input)
        /// → band index 0..n. Shared by DistanceBand and ScaledMagnitude.
        /// </summary>
        public static int Bucket(float value, ref BlobArray<float> thresholds)
        {
            var band = 0;
            for (var k = 0; k < thresholds.Length; k++)
            {
                if (value <= thresholds[k])
                {
                    break;
                }

                band++;
            }

            return band;
        }

        /// <summary>
        /// Map a BelongsTo bitmask to an ordinal via parallel mask/ordinal tier arrays.
        /// First matching mask wins; no match → <paramref name="fallback"/>.
        /// </summary>
        public static int CategoryOrdinal(uint belongsTo, ref BlobArray<uint> masks, ref BlobArray<int> ordinals,
            int fallback)
        {
            for (var k = 0; k < masks.Length; k++)
            {
                if ((belongsTo & masks[k]) != 0)
                {
                    return ordinals[k];
                }
            }

            return fallback;
        }

        /// <summary>
        /// Sign + magnitude band of a closing speed: sign(closing) * (1 + floor(|closing| / bandWidth)).
        /// 0 when |closing| is below half a band so a near-stationary body reads as 0, not a spurious ±1.
        /// </summary>
        public static int ApproachVelocityBand(float closingSpeed, float bandWidth)
        {
            if (bandWidth <= Epsilon)
            {
                return (int)math.sign(closingSpeed);
            }

            var mag = math.abs(closingSpeed);
            if (mag < bandWidth * 0.5f)
            {
                return 0;
            }

            var band = 1 + (int)math.floor((mag - bandWidth * 0.5f) / bandWidth);
            return (int)math.sign(closingSpeed) * band;
        }

        // ---------------------------------------------------------------------------------------------------
        // WAVE 3 — pure math (collision-normal sector, impact band, perpendicular distance, timing grade).
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Quantize a collision <paramref name="normal"/> into a sector/face index using the SAME planar sector
        /// math as <see cref="ComputeSector"/>. The normal is treated as a direction (an offset from origin).
        /// Degenerate (|normal| &lt; eps) → sentinel == sectorCount. Shared by ContactNormalSector / DeflectionBounce.
        /// </summary>
        public static int NormalSector(float3 normal, float3 fwd, float3 up, int sectorCount)
        {
            return ComputeSector(normal, fwd, up, sectorCount);
        }

        /// <summary>
        /// Reflect <paramref name="velocity"/> across a contact <paramref name="normal"/> (the standard
        /// v - 2(v·n)n bounce) and quantize the resulting direction into a sector. Degenerate normal/velocity →
        /// sentinel == sectorCount.
        /// </summary>
        public static int DeflectionSector(float3 velocity, float3 normal, float3 fwd, float3 up, int sectorCount)
        {
            var n = math.normalizesafe(normal, float3.zero);
            if (math.lengthsq(n) < Epsilon * Epsilon || math.lengthsq(velocity) < Epsilon * Epsilon)
            {
                return sectorCount; // sentinel — no usable reflection
            }

            var reflected = velocity - 2f * math.dot(velocity, n) * n;
            return ComputeSector(reflected, fwd, up, sectorCount);
        }

        /// <summary>
        /// Bucket a raw collision impulse magnitude into ascending bands (NOT squared — impulse is already a scalar).
        /// Shared with <see cref="Bucket"/>; this overload is the named entry point for ImpactBand / SpeedBand.
        /// </summary>
        public static int ImpactBand(float impulse, ref BlobArray<float> thresholds)
        {
            return Bucket(impulse, ref thresholds);
        }

        /// <summary>
        /// Perpendicular distance from <paramref name="point"/> to the infinite line through <paramref name="a"/>
        /// in direction <paramref name="segDir"/> (vector reject). Degenerate segDir → straight-line distance.
        /// Used by MostBlocking / RacingLineThreat to score bodies near the self→reference line.
        /// </summary>
        public static float PerpendicularDistance(float3 point, float3 a, float3 segDir)
        {
            var ap = point - a;
            var dirLenSq = math.lengthsq(segDir);
            if (dirLenSq < Epsilon * Epsilon)
            {
                return math.length(ap); // no segment direction → just the radial distance
            }

            var dir = segDir * math.rsqrt(dirLenSq);
            var along = math.dot(ap, dir);
            var reject = ap - along * dir;
            return math.length(reject);
        }

        /// <summary>
        /// Grade a clip-local normalized time <paramref name="t"/> (0..1) against an authored beat centre into an
        /// ordinal: 0 = Perfect, 1 = Great, 2 = Good, 3 = Late/Miss. <paramref name="windows"/> are ascending
        /// half-widths (|t - beat| &lt;= windows[k] → grade k). Reuses the simple band shape of TimingWindowGrade.
        /// </summary>
        public static int TimingWindowGrade(float t, float beatCenter, float perfect, float great, float good)
        {
            var delta = math.abs(t - beatCenter);
            if (delta <= perfect)
            {
                return 0;
            }

            if (delta <= great)
            {
                return 1;
            }

            if (delta <= good)
            {
                return 2;
            }

            return 3; // outside all windows — Late / Miss
        }

        /// <summary>
        /// Combined heaviest-mover / most-momentum score: mass^a * |v|^b. With a = 1, b = 0 it is pure mass;
        /// a = 0, b = 1 is pure speed; a = 1, b = 1 is momentum. Mass &lt;= 0 (static / unknown) contributes
        /// mass term 1 so a static body still scores by speed without poisoning the product.
        /// </summary>
        public static float HeaviestMoverScore(float mass, float speed, float massExp, float speedExp)
        {
            var m = mass > Epsilon ? math.pow(mass, massExp) : (massExp == 0f ? 1f : 0f);
            var v = speed > Epsilon ? math.pow(speed, speedExp) : (speedExp == 0f ? 1f : 0f);
            return m * v;
        }

        /// <summary>
        /// The DwellToAcquire counter transition for one frame: increment the consecutive-present dwell when the
        /// candidate is <paramref name="seenThisFrame"/>, otherwise reset to 0. Saturates at ushort.MaxValue.
        /// Mirrors the system's tracked-list dwell bookkeeping so the acquire gate (dwell &gt;= N) is unit-testable.
        /// </summary>
        public static ushort NextDwell(ushort current, bool seenThisFrame)
        {
            if (!seenThisFrame)
            {
                return 0;
            }

            return current < ushort.MaxValue ? (ushort)(current + 1) : current;
        }

        /// <summary> True when a candidate has dwelt long enough to win (dwell &gt;= required, required 0 = no gate). </summary>
        public static bool DwellAcquired(ushort dwell, ushort required)
        {
            return required == 0 || dwell >= required;
        }
    }
}
