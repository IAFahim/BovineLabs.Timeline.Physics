using System;
using System.Collections.Generic;
using BovineLabs.Timeline.Core.Editor.CliTools.Shared;
using BovineLabs.Timeline.Physics.Authoring;
using Newtonsoft.Json.Linq;

namespace BovineLabs.Timeline.Physics.Editor.CliTools
{
    /// <summary>
    /// The hand-declared (non-reflectable) requirements for a StatefulTrigger spawn clip — the silent
    /// traps a designer hits: the SOURCE shape must Raise Trigger Events, and the spawned
    /// ObjectDefinition must exist, be registered, and back-link to its prefab. Lives in the physics
    /// package (next to the clip it knows about); the engine unions these with the reflectable
    /// requirements (track, binding, clip, exposed-refs) derived from the clip/track types.
    ///
    /// Scope note (first slice): the payload prefab's internal reaction/action authoring (the "what the
    /// hit does") is the payload's own concern and is NOT declared here — this fragment owns the
    /// trigger-wiring + spawn-registration surface, which is where the silent failures live.
    /// </summary>
    [UnityCliManifest]
    internal sealed class PhysicsTriggerInstantiateManifest : IMechanicManifest
    {
        public bool Handles(Type clipType) => clipType == typeof(PhysicsTriggerInstantiateClip);

        public IEnumerable<Requirement> Requirements(ManifestContext ctx)
        {
            var p = ctx.P;
            var reqs = new List<Requirement>();

            // Spawn registration: the ObjectDefinition the clip references must exist, be registered, and
            // its prefab back-link must point home (the two silent spawn traps).
            string objdef = p.OptString("objdef");
            string prefab = p.OptString("prefab");
            if (!string.IsNullOrEmpty(objdef) && !string.IsNullOrEmpty(prefab))
            {
                var objdefParams = new JObject { ["definition"] = objdef, ["prefab"] = prefab };
                var friendly = p.OptString("friendly_name");
                if (!string.IsNullOrEmpty(friendly)) objdefParams["friendly_name"] = friendly;
                reqs.Add(new Requirement("ensure_objdef", objdefParams,
                    $"object definition {objdef} -> {prefab}", ReqPhase.Assets));
            }

            // Trigger SOURCE: every collider shape must Raise Trigger Events or the clip never fires.
            string source = p.OptString("source") ?? p.OptString("bind_object");
            if (!string.IsNullOrEmpty(source))
                reqs.Add(new Requirement("ensure_trigger_source", new JObject
                {
                    ["subscene"] = ctx.Subscene,
                    ["object"] = source,
                }, $"trigger source {source} raises trigger events", ReqPhase.SceneSetup));

            return reqs;
        }
    }
}
