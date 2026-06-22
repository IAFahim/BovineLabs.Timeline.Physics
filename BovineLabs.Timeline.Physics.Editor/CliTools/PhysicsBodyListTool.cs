using System.Collections.Generic;
using System.IO;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using Newtonsoft.Json.Linq;
using Unity.Physics.Authoring;
using UnityCliConnector;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Editor.CliTools
{
    [UnityCliTool(
        Name = "physics_body_list",
        Group = "vex",
        Description =
            "Per PhysicsBodyAuthoring holder in the SubScene: path, MotionType, Mass, shape summary (PhysicsShapeAuthoring collider type), and ForceUnique (the §3.4 filter-override trap). Read-only discovery.")]
    public static class PhysicsBodyListTool
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new Params(@params);
            try
            {
                using (var session = SubSceneSession.Open(p.OptString("subscene")))
                {
                    if (session.Error != null) return session.Error;

                    var bodies = new List<object>();
                    var all = Object.FindObjectsByType<PhysicsBodyAuthoring>(FindObjectsInactive.Include);
                    foreach (var body in all)
                    {
                        if (body.gameObject.scene != session.Subscene) continue;

                        var shapes = body.GetComponentsInChildren<PhysicsShapeAuthoring>(true);
                        var shapeSummaries = new List<object>();
                        foreach (var shape in shapes)
                            shapeSummaries.Add(new
                            {
                                path = Hierarchy.PathOf(shape.gameObject),
                                colliderType = shape.ShapeType.ToString(),
                                forceUnique = shape.ForceUnique
                            });

                        bodies.Add(new
                        {
                            path = Hierarchy.PathOf(body.gameObject),
                            motionType = body.MotionType.ToString(),
                            mass = body.Mass,
                            shapeCount = shapeSummaries.Count,
                            shapes = shapeSummaries
                        });
                    }

                    var sceneName = Path.GetFileNameWithoutExtension(session.SubscenePath);
                    return ToolEnvelope.Ok(
                        $"{bodies.Count} physics body holder(s) in '{sceneName}'.",
                        new { subscene = session.SubscenePath, bodies });
                }
            }
            catch (ToolException e)
            {
                return ToolEnvelope.FromException(e);
            }
        }

        public class Parameters
        {
            [ToolParameter("Subscene .unity path. Default: auto-detected from the active scene's SubScene component.")]
            public string Subscene { get; set; }
        }
    }
}