using System;
using System.Collections.Generic;
using BovineLabs.Core.Keys;
using BovineLabs.Core.Settings;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    /// <summary>
    ///     Registry of all <see cref="SplineSchema" /> assets (auto-populated by the AutoRef on the schema), exposing
    ///     a Burst-safe name→key lookup via <c>K&lt;SplineSettings&gt;</c>. Surfaced in the BL settings window for
    ///     easy designer tooling. Same KSettingsBase pattern as EntityLinkSettings.
    /// </summary>
    [SettingsGroup("Splines")]
    public sealed class SplineSettings : KSettingsBase<SplineSettings, ushort>
    {
        [SerializeField] private SplineSchema[] splines = Array.Empty<SplineSchema>();

        public IReadOnlyList<SplineSchema> Splines => splines;

        public override IEnumerable<NameValue<ushort>> Keys
        {
            get
            {
                foreach (var schema in splines)
                {
                    if (schema == null)
                    {
                        continue;
                    }

                    yield return new NameValue<ushort>(schema.name, schema.Id);
                }
            }
        }
    }
}
