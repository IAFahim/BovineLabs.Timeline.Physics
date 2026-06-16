using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityCliConnector;
using UnityEditor;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Editor.CliTools
{
    [UnityCliTool(
        Name = "ensure_trigger_source",
        Group = "vex",
        Description = "Idempotent: ensure a SubScene object is a working trigger SOURCE — every PhysicsShapeAuthoring under it set to Raise Trigger Events (override on). Fixes the #1 silent trap where a StatefulTrigger clip never fires because the shape only collides. Delegates the field writes to ensure_component, so the undo is the Core-registered ensure_component inverse. dry_run reports without mutating.")]
    public static class EnsureTriggerSourceTool
    {
        // Serialized path of the override flag + value inside PhysicsShapeAuthoring.m_Material.
        private const string OverridePath = "m_Material.m_CollisionResponse.m_Override";
        private const string ValuePath = "m_Material.m_CollisionResponse.m_Value";

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected.")]
            public string Subscene { get; set; }

            [ToolParameter("The trigger SOURCE object (carries/parents the collider shapes).", Required = true)]
            public string Object { get; set; }

            [ToolParameter("Report-only: check satisfaction without mutating (default false).")]
            public bool DryRun { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                var guard = AssetUtil.PlayModeBlocked();
                if (guard != null) return guard;

                string objPath = p.RequireString("object");
                bool dryRun = p.OptBool("dry_run", false);
                string subscene = p.OptString("subscene");
                int wantValue = (int)CollisionResponsePolicy.RaiseTriggerEvents;
                var target = new { @object = objPath };

                // Discover the shape object paths + current state in one short session, then delegate
                // outside it (so the per-shape ensure_component sessions don't nest).
                var shapePaths = new List<string>();
                var notOk = new List<string>();
                var compound = new List<string>();
                using (var session = SubSceneSession.Open(subscene))
                {
                    if (session.Error != null) return session.Error;
                    subscene = session.SubscenePath;

                    var go = session.Find(objPath);
                    if (go == null) return ToolEnvelope.Error("NOT_FOUND", $"No object '{objPath}' in {session.SubscenePath}.");

                    var shapes = go.GetComponentsInChildren<PhysicsShapeAuthoring>(true);
                    if (shapes.Length == 0)
                        return ToolEnvelope.Error("MISSING_PREREQUISITE",
                            $"'{objPath}' has no PhysicsShapeAuthoring collider — a trigger source needs at least one.");

                    var perPath = new Dictionary<string, int>();
                    foreach (var shape in shapes)
                    {
                        string path = Hierarchy.PathOf(shape.gameObject);
                        perPath[path] = perPath.TryGetValue(path, out var c) ? c + 1 : 1;
                        if (!shapePaths.Contains(path)) shapePaths.Add(path);
                        var so = new SerializedObject(shape);
                        bool ov = so.FindProperty(OverridePath)?.boolValue ?? false;
                        int val = so.FindProperty(ValuePath)?.intValue ?? -1;
                        if (!(ov && val == wantValue) && !notOk.Contains(path)) notOk.Add(path);
                    }

                    // Only a compound node that still NEEDS a fix is ambiguous; an already-correct
                    // compound node must fall through to the Satisfied return below (stay idempotent).
                    foreach (var kv in perPath)
                        if (kv.Value > 1 && notOk.Contains(kv.Key)) compound.Add(kv.Key);
                }

                // ensure_component resolves a component by hierarchy path and would only fix the FIRST shape
                // when several resolve to the same path — whether that is a compound node (multiple shapes on
                // one GameObject) OR duplicate-named sibling objects. Both are unfixable by path delegation,
                // so fail loudly instead of reporting a half-fix as success.
                if (compound.Count > 0)
                    return ToolEnvelope.Error("AMBIGUOUS",
                        $"Multiple PhysicsShapeAuthoring under '{objPath}' resolve to the same hierarchy path ({string.Join(", ", compound)}) — " +
                        "either a compound node (several shapes on one GameObject) or duplicate-named sibling objects. This tool resolves " +
                        "shapes by path and cannot target them individually; give them distinct names / split onto separate objects, or set " +
                        "Raise Trigger Events on them manually.",
                        new { ambiguousPaths = compound.ToArray() });

                if (notOk.Count == 0)
                    return EnsureResult.Satisfied($"'{objPath}' already raises trigger events on {shapePaths.Count} shape(s).",
                        target, new { shapes = shapePaths });

                if (dryRun)
                    return EnsureResult.WouldFixResult($"Would set Raise Trigger Events on {notOk.Count} shape(s) of '{objPath}'.",
                        target, new { shapes = shapePaths, needFix = notOk });

                var undo = new List<object>();
                foreach (var path in notOk)
                {
                    var resp = EnsureComponentTool.HandleCommand(new JObject
                    {
                        ["subscene"] = subscene,
                        ["object"] = path,
                        ["component"] = "PhysicsShapeAuthoring",
                        ["fields"] = new JObject
                        {
                            [OverridePath] = true,
                            [ValuePath] = wantValue,
                        },
                    });
                    if (Responses.IsError(resp)) { Rollback(undo); return resp; }
                    var u = Responses.Undo(resp);
                    if (u != null) undo.AddRange(u);
                }

                return EnsureResult.Applied($"Set Raise Trigger Events on {notOk.Count} shape(s) of '{objPath}'.",
                    target, new { needFix = notOk }, new { fixed_shapes = notOk }, undo.ToArray());
            }
            catch (ToolException e) { return ToolEnvelope.FromException(e); }
        }

        private static void Rollback(List<object> undo)
        {
            if (undo.Count == 0) return;
            var rev = new List<object>(undo);
            rev.Reverse();
            ToolUndoTool.HandleCommand(new JObject { ["undo"] = JArray.FromObject(rev) });
        }
    }
}
