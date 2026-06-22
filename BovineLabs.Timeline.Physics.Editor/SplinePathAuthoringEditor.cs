using System.Collections.Generic;
using System.IO;
using BovineLabs.Core.Collections;
using BovineLabs.Timeline.Physics.Authoring.Splines;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
namespace BovineLabs.Timeline.Physics.Editor
{
    /// <summary>
    /// Inspector for <see cref="SplinePathAuthoring"/>. By default it visualises the *baked* <see cref="BlobSpline"/>
    /// pulled from ECS (a live world if one exists, otherwise by running the real bake at edit time) so you see the
    /// path the runtime actually follows — including its baked arc length. A toggle falls back to the raw authoring
    /// curves. Either source can be exported as a standalone SVG (see <see cref="SplinePathSvg"/>).
    /// </summary>
    [CustomEditor(typeof(SplinePathAuthoring))]
    public sealed class SplinePathAuthoringEditor : UnityEditor.Editor
    {
        private const int SamplesPerCurve = 18;

        private static readonly string[] SourceTabs = { "Baked (ECS)", "Authoring" };
        private bool useAuthoring;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (SplinePathAuthoring)this.target;
            var container = authoring.GetComponent<SplineContainer>();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Spline Path — SVG Preview", EditorStyles.boldLabel);

            if (container == null || container.Splines.Count == 0)
            {
                EditorGUILayout.HelpBox("No spline knots yet — edit the SplineContainer to draw a path.", MessageType.Info);
                return;
            }

            this.useAuthoring = GUILayout.Toolbar(this.useAuthoring ? 1 : 0, SourceTabs) == 1;

            if (!this.useAuthoring && authoring.schema == null)
            {
                EditorGUILayout.HelpBox(
                    "No SplineSchema assigned — the path can still be baked for preview, but spline-follow clips " +
                    "won't find it at runtime.", MessageType.Warning);
            }

            if (!TryResolve(authoring, container, out var paths, out var sourceLabel, out var length))
            {
                EditorGUILayout.HelpBox("Could not read a path from the selected source.", MessageType.Info);
                return;
            }

            if (!SplinePathSvg.TryProject(paths, out var proj))
            {
                EditorGUILayout.HelpBox("The spline has no curves to draw.", MessageType.Info);
                return;
            }

            DrawPreview(proj);

            var knots = 0;
            foreach (var p in paths)
            {
                knots += p.Knots.Count;
            }

            EditorGUILayout.LabelField($"source: {sourceLabel}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"plane {proj.PlaneLabel}   ·   {knots} knots   ·   length ≈ {length:0.##} m",
                EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save SVG…"))
                {
                    SaveSvg(authoring, proj);
                }

                if (GUILayout.Button("Copy SVG"))
                {
                    EditorGUIUtility.systemCopyBuffer = SplinePathSvg.Build(proj);
                    Notify("Spline path SVG copied to clipboard");
                }
            }
        }

        /// <summary>Resolve the chosen data source into world-space paths plus a human label and arc length.</summary>
        private bool TryResolve(
            SplinePathAuthoring authoring, SplineContainer container,
            out List<SplinePathSvg.WorldPath> paths, out string sourceLabel, out float length)
        {
            if (!this.useAuthoring)
            {
                // 1. Prefer a live baked entity (play mode / open baked SubScene), keyed by the schema id.
                if (authoring.schema != null &&
                    TryGetLiveBlob(authoring.schema.Id, out var live) && live.IsCreated)
                {
                    paths = FromBlob(live, out length);
                    sourceLabel = "live ECS world (baked blob)";
                    return paths.Count > 0;
                }

                // 2. Otherwise run the real bake path at edit time — same BlobSpline.Create the baker uses.
                var transform = (float4x4)container.transform.localToWorldMatrix;
                var blob = BlobSpline.Create(container.Splines[0], transform, Allocator.Persistent);
                try
                {
                    paths = FromBlob(blob, out length);
                    sourceLabel = "baked blob (edit-time, BlobSpline.Create)";
                    return paths.Count > 0;
                }
                finally
                {
                    if (blob.IsCreated)
                    {
                        blob.Dispose();
                    }
                }
            }

            paths = SplinePathSvg.FromContainer(container);
            sourceLabel = "authoring SplineContainer (all splines)";
            length = 0f;
            foreach (var spline in container.Splines)
            {
                length += SplineUtility.CalculateLength(spline, (float4x4)container.transform.localToWorldMatrix);
            }

            return paths.Count > 0;
        }

        private static List<SplinePathSvg.WorldPath> FromBlob(BlobAssetReference<BlobSpline> blob, out float length)
        {
            ref var bs = ref blob.Value;
            length = bs.Length;

            var wp = new SplinePathSvg.WorldPath { Closed = bs.Closed };
            for (var i = 0; i < bs.Curves.Length; i++)
            {
                var c = bs.Curves[i];
                wp.Curves.Add(new SplinePathSvg.WorldCurve { P0 = c.P0, P1 = c.P1, P2 = c.P2, P3 = c.P3 });
            }

            for (var k = 0; k < bs.Knots.Length; k++)
            {
                wp.Knots.Add(bs.Knots[k].Position);
            }

            return new List<SplinePathSvg.WorldPath> { wp };
        }

        private static bool TryGetLiveBlob(ushort id, out BlobAssetReference<BlobSpline> blob)
        {
            blob = default;
            foreach (var world in World.All)
            {
                if (!world.IsCreated)
                {
                    continue;
                }

                var em = world.EntityManager;
                using var query = em.CreateEntityQuery(
                    ComponentType.ReadOnly<SplineBlob>(), ComponentType.ReadOnly<SplineKey>());
                if (query.IsEmptyIgnoreFilter)
                {
                    continue;
                }

                using var keys = query.ToComponentDataArray<SplineKey>(Allocator.Temp);
                using var blobs = query.ToComponentDataArray<SplineBlob>(Allocator.Temp);
                for (var i = 0; i < keys.Length; i++)
                {
                    if (keys[i].Value == id && blobs[i].Value.IsCreated)
                    {
                        blob = blobs[i].Value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void DrawPreview(SplinePathSvg.Projection proj)
        {
            var maxWidth = Mathf.Min(EditorGUIUtility.currentViewWidth - 40f, 260f);
            var aspect = proj.Height / Mathf.Max(proj.Width, 1e-3f);
            var rect = GUILayoutUtility.GetRect(maxWidth, maxWidth * aspect);

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.082f, 0.082f, 0.102f));

            // Fit the viewBox into the reserved rect with a uniform, centred scale.
            var s = Mathf.Min(rect.width / proj.Width, rect.height / proj.Height);
            var offset = new Vector2(
                rect.x + ((rect.width - (proj.Width * s)) * 0.5f),
                rect.y + ((rect.height - (proj.Height * s)) * 0.5f));

            Vector3 ToView(Vector2 p)
            {
                return new Vector3(offset.x + (p.x * s), offset.y + (p.y * s), 0f);
            }

            var stroke = new Color(0.5f, 0.83f, 1f);
            foreach (var path in proj.Paths)
            {
                if (path.Curves.Count == 0)
                {
                    continue;
                }

                var points = new Vector3[(path.Curves.Count * SamplesPerCurve) + 1];
                var idx = 0;
                points[idx++] = ToView(path.Curves[0].P0);
                foreach (var c in path.Curves)
                {
                    for (var i = 1; i <= SamplesPerCurve; i++)
                    {
                        points[idx++] = ToView(SplinePathSvg.Evaluate(c, (float)i / SamplesPerCurve));
                    }
                }

                Handles.color = stroke;
                Handles.DrawAAPolyLine(2f, points);

                foreach (var knot in path.Knots)
                {
                    var k = ToView(knot);
                    EditorGUI.DrawRect(new Rect(k.x - 2f, k.y - 2f, 4f, 4f), new Color(0.81f, 0.82f, 0.84f));
                }
            }

            var start = ToView(proj.Start);
            EditorGUI.DrawRect(new Rect(start.x - 3f, start.y - 3f, 6f, 6f), new Color(0.36f, 0.84f, 0.36f));
            if (proj.HasEnds)
            {
                var end = ToView(proj.End);
                EditorGUI.DrawRect(new Rect(end.x - 3f, end.y - 3f, 6f, 6f), new Color(1f, 0.42f, 0.42f));
            }
        }

        private static void SaveSvg(SplinePathAuthoring authoring, SplinePathSvg.Projection proj)
        {
            var defaultName = authoring.gameObject.name + "_path.svg";
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Spline Path SVG", defaultName, "svg",
                "Choose where to write the spline path SVG.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, SplinePathSvg.Build(proj));
            AssetDatabase.ImportAsset(path);

            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

            Debug.Log(
                $"[SplinePath] Wrote SVG → {path}. To use it in UI Toolkit, select the asset and set its " +
                "importer 'Generated Asset Type' to 'UI Toolkit Vector Image'.",
                asset);
            Notify("Saved " + path);
        }

        private static void Notify(string message)
        {
            var window = EditorWindow.focusedWindow;
            if (window != null)
            {
                window.ShowNotification(new GUIContent(message));
            }
        }
    }
}
#endif
