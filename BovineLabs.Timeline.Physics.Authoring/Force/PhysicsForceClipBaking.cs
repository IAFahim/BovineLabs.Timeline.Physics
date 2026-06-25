using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Shared bake ceremony for the focused force clips (Knockback / Thrust / Vortex). Keeps the per-clip authoring
    /// classes small and focused while routing through the same <see cref="PhysicsTriggerForceBuilder"/> the legacy
    /// multi-mode clip used — so the baked output and runtime are identical.
    /// </summary>
    internal static class PhysicsForceClipBaking
    {
        public static void Bake(
            ref BakerCommands commands, BakingContext context,
            PhysicsTriggerForceType forceType, StatefulEventState triggerState, PhysicsForceMode mode,
            float magnitude, float3 direction, PhysicsTriggerPositionMode originMode,
            PhysicsTriggerFalloffCurve falloffCurve, float falloffStartRadius, float falloffEndRadius,
            StatSchemaObject strengthStat, Target readStatFrom, EntityLinkSchema readStatLink,
            Target applyTo, EntityLinkSchema applyToLink,
            Target ignoreTarget, EntityLinkSchema[] requireLinks, PhysicsTriggerHitMode hitMode)
        {
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            ushort applyToKey = 0;
            if (applyToLink != null && EntityLinkAuthoringUtility.TryGetKey(applyToLink, out var k2))
                applyToKey = k2;

            // Continuous forces accumulate every frame the clip is active, so they must match on Stay.
            var bakedState = triggerState;
            if (mode == PhysicsForceMode.Continuous && bakedState != StatefulEventState.Stay)
                bakedState = StatefulEventState.Stay;

            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, requireLinks);

            var builder = new PhysicsTriggerForceBuilder
            {
                ForceData = new PhysicsTriggerForceData
                {
                    EventState = bakedState,
                    ForceType = forceType,
                    Mode = mode,
                    Magnitude = magnitude,
                    Direction = math.normalizesafe(direction, new float3(0, 0, 1)),
                    OriginMode = originMode,
                    FalloffCurve = falloffCurve,
                    FalloffStartRadius = falloffStartRadius,
                    FalloffEndRadius = falloffEndRadius,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey,
                    },
                    ApplyTo = applyTo,
                    ApplyToLinkKey = applyToKey,
                },
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = ignoreTarget,
                    LinkFilterBlob = filterBlob,
                    HitMode = hitMode,
                },
            };
            builder.ApplyTo(ref commands);
        }
    }
}
