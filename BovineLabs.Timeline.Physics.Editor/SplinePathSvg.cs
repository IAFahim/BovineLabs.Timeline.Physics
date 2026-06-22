using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    /// <summary>
    /// Projects world-space spline paths onto their dominant 2D plane and emits a standalone SVG.
    /// Input is a set of <see cref="WorldPath"/>s (world-space cubic beziers) so the same renderer serves
    /// both the authoring <see cref="SplineContainer"/> and the baked <c>BlobSpline</c> read out of ECS.
    /// The path is written as exact cubic-bezier <c>C</c> commands — the SVG IS the spline. Set the saved
    /// asset's importer "Generated Asset Type" to "UI Toolkit Vector Image" to use it as a UITK VectorImage.
    /// </summary>
    internal static class SplinePathSvg
    {
        private const float TargetSize = 256f;
        private const float Pad = 14f;

        // ---- world-space input (source-agnostic) ------------------------------------------------

        internal struct WorldCurve
        {
            public float3 P0;
            public float3 P1;
            public float3 P2;
            public float3 P3;
        }

        internal sealed class WorldPath
        {
            public readonly List<WorldCurve> Curves = new();
            public readonly List<float3> Knots = new();
            public bool Closed;
        }

        // ---- projected (2D) result --------------------------------------------------------------

        internal struct Curve2D
        {
            public Vector2 P0;
            public Vector2 P1;
            public Vector2 P2;
            public Vector2 P3;
        }

        internal sealed class SplinePath2D
        {
            public readonly List<Curve2D> Curves = new();
            public readonly List<Vector2> Knots = new();
            public bool Closed;
        }

        internal sealed class Projection
        {
            public float Width;
            public float Height;
            public string PlaneLabel;
            public readonly List<SplinePath2D> Paths = new();
            public Vector2 Start;
            public Vector2 End;
            public bool HasEnds;
        }

        /// <summary>Adapt an authoring <see cref="SplineContainer"/> into world-space paths (all splines).</summary>
        internal static List<WorldPath> FromContainer(SplineContainer container)
        {
            var result = new List<WorldPath>();
            if (container == null)
            {
                return result;
            }

            var m = (float4x4)container.transform.localToWorldMatrix;
            foreach (var spline in container.Splines)
            {
                var wp = new WorldPath { Closed = spline.Closed };

                var curveCount = spline.GetCurveCount();
                for (var i = 0; i < curveCount; i++)
                {
                    var c = spline.GetCurve(i);
                    wp.Curves.Add(new WorldCurve
                    {
                        P0 = math.transform(m, c.P0),
                        P1 = math.transform(m, c.P1),
                        P2 = math.transform(m, c.P2),
                        P3 = math.transform(m, c.P3),
                    });
                }

                for (var k = 0; k < spline.Count; k++)
                {
                    wp.Knots.Add(math.transform(m, spline[k].Position));
                }

                result.Add(wp);
            }

            return result;
        }

        internal static bool TryProject(IReadOnlyList<WorldPath> worldPaths, out Projection proj)
        {
            proj = null;
            if (worldPaths == null || worldPaths.Count == 0)
            {
                return false;
            }

            // 1. Bounds over every control point, to size the viewBox and pick the plane.
            var has = false;
            float3 min = default, max = default;
            foreach (var path in worldPaths)
            {
                foreach (var c in path.Curves)
                {
                    Accumulate(ref min, ref max, ref has, c.P0);
                    Accumulate(ref min, ref max, ref has, c.P1);
                    Accumulate(ref min, ref max, ref has, c.P2);
                    Accumulate(ref min, ref max, ref has, c.P3);
                }
            }

            if (!has)
            {
                return false;
            }

            var ext = max - min;

            // 2. Keep the two largest-extent axes, in index order so a ground path draws X horizontal / Z vertical.
            var e = new[] { ext.x, ext.y, ext.z };
            var largest = 0;
            for (var i = 1; i < 3; i++)
            {
                if (e[i] > e[largest])
                {
                    largest = i;
                }
            }

            var second = -1;
            for (var i = 0; i < 3; i++)
            {
                if (i == largest)
                {
                    continue;
                }

                if (second == -1 || e[i] > e[second])
                {
                    second = i;
                }
            }

            var h = math.min(largest, second);
            var v = math.max(largest, second);

            var span = math.max(e[h], e[v]);
            if (span < 1e-5f)
            {
                span = 1f;
            }

            var scale = (TargetSize - (2f * Pad)) / span;
            proj = new Projection
            {
                Width = (e[h] * scale) + (2f * Pad),
                Height = (e[v] * scale) + (2f * Pad),
                PlaneLabel = $"{AxisName(h)} / {AxisName(v)}",
            };

            Vector2 Map(float3 wp)
            {
                var x = ((wp[h] - min[h]) * scale) + Pad;
                var y = ((max[v] - wp[v]) * scale) + Pad; // flip vertical: SVG y grows downward
                return new Vector2(x, y);
            }

            for (var s = 0; s < worldPaths.Count; s++)
            {
                var src = worldPaths[s];
                var path = new SplinePath2D { Closed = src.Closed };

                foreach (var c in src.Curves)
                {
                    path.Curves.Add(new Curve2D
                    {
                        P0 = Map(c.P0),
                        P1 = Map(c.P1),
                        P2 = Map(c.P2),
                        P3 = Map(c.P3),
                    });
                }

                foreach (var knot in src.Knots)
                {
                    path.Knots.Add(Map(knot));
                }

                proj.Paths.Add(path);

                if (s == 0 && path.Curves.Count > 0)
                {
                    proj.Start = path.Curves[0].P0;
                    proj.End = path.Curves[^1].P3;
                    proj.HasEnds = !src.Closed;
                }
            }

            return true;
        }

        internal static string Build(Projection proj)
        {
            if (proj == null)
            {
                return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\" width=\"16\" height=\"16\"/>";
            }

            var w = proj.Width;
            var h = proj.Height;

            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
                .Append(N(w)).Append(' ').Append(N(h))
                .Append("\" width=\"").Append(N(w)).Append("\" height=\"").Append(N(h)).Append("\">\n");
            sb.Append("  <rect x=\"0\" y=\"0\" width=\"").Append(N(w)).Append("\" height=\"").Append(N(h))
                .Append("\" rx=\"6\" fill=\"#15151a\"/>\n");

            foreach (var path in proj.Paths)
            {
                if (path.Curves.Count == 0)
                {
                    continue;
                }

                sb.Append("  <path d=\"M ").Append(P(path.Curves[0].P0));
                foreach (var c in path.Curves)
                {
                    sb.Append(" C ").Append(P(c.P1)).Append(' ').Append(P(c.P2)).Append(' ').Append(P(c.P3));
                }

                if (path.Closed)
                {
                    sb.Append(" Z");
                }

                sb.Append("\" fill=\"").Append(path.Closed ? "#7fd4ff22" : "none")
                    .Append("\" stroke=\"#7fd4ff\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
            }

            foreach (var path in proj.Paths)
            {
                foreach (var knot in path.Knots)
                {
                    sb.Append("  <circle cx=\"").Append(N(knot.x)).Append("\" cy=\"").Append(N(knot.y))
                        .Append("\" r=\"2.5\" fill=\"#cfd2d6\"/>\n");
                }
            }

            sb.Append("  <circle cx=\"").Append(N(proj.Start.x)).Append("\" cy=\"").Append(N(proj.Start.y))
                .Append("\" r=\"4\" fill=\"#5dd55d\"/>\n");
            if (proj.HasEnds)
            {
                sb.Append("  <circle cx=\"").Append(N(proj.End.x)).Append("\" cy=\"").Append(N(proj.End.y))
                    .Append("\" r=\"4\" fill=\"#ff6b6b\"/>\n");
            }

            sb.Append("  <text x=\"8\" y=\"").Append(N(h - 7f))
                .Append("\" font-family=\"monospace\" font-size=\"9\" fill=\"#888\">plane ")
                .Append(proj.PlaneLabel).Append("</text>\n");

            sb.Append("</svg>\n");
            return sb.ToString();
        }

        /// <summary>Evaluate a projected cubic bezier at t via de Casteljau (used by the live inspector preview).</summary>
        internal static Vector2 Evaluate(in Curve2D c, float t)
        {
            var a = Vector2.LerpUnclamped(c.P0, c.P1, t);
            var b = Vector2.LerpUnclamped(c.P1, c.P2, t);
            var d = Vector2.LerpUnclamped(c.P2, c.P3, t);
            var ab = Vector2.LerpUnclamped(a, b, t);
            var bd = Vector2.LerpUnclamped(b, d, t);
            return Vector2.LerpUnclamped(ab, bd, t);
        }

        private static void Accumulate(ref float3 min, ref float3 max, ref bool has, float3 p)
        {
            if (!has)
            {
                min = p;
                max = p;
                has = true;
                return;
            }

            min = math.min(min, p);
            max = math.max(max, p);
        }

        private static string AxisName(int axis)
        {
            return axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
        }

        private static string P(Vector2 p)
        {
            return N(p.x) + "," + N(p.y);
        }

        private static string N(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
#endif
