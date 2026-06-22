using System;
using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using BovineLabs.Timeline.Physics.Authoring;
using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Physics.Editor.CliTools
{
    [UnityCliManifest]
    internal sealed class PhysicsTriggerInstantiateManifest : IMechanicManifest
    {
        public bool Handles(Type clipType)
        {
            return clipType == typeof(PhysicsTriggerInstantiateClip);
        }

        public IEnumerable<Requirement> Requirements(ManifestContext ctx)
        {
            var p = ctx.P;
            var reqs = new List<Requirement>();

            var objdef = p.OptString("objdef");
            var prefab = p.OptString("prefab");
            if (!string.IsNullOrEmpty(objdef) && !string.IsNullOrEmpty(prefab))
            {
                var objdefParams = new JObject { ["definition"] = objdef, ["prefab"] = prefab };
                var friendly = p.OptString("friendly_name");
                if (!string.IsNullOrEmpty(friendly)) objdefParams["friendly_name"] = friendly;
                reqs.Add(new Requirement("ensure_objdef", objdefParams,
                    $"object definition {objdef} -> {prefab}", ReqPhase.Assets));
            }

            var source = p.OptString("source") ?? p.OptString("bind_object");
            if (!string.IsNullOrEmpty(source))
                reqs.Add(new Requirement("ensure_trigger_source", new JObject
                    {
                        ["subscene"] = ctx.Subscene,
                        ["object"] = source
                    }, $"trigger source {source} raises trigger events", ReqPhase.SceneSetup));

            return reqs;
        }
    }
}