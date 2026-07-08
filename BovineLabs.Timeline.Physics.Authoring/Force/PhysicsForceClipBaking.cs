using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
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
            MotionChannel channel,
            float magnitude, float3 direction, PhysicsTriggerPositionMode originMode,
            PhysicsTriggerFalloffCurve falloffCurve, float falloffStartRadius, float falloffEndRadius,
            StatSchemaObject strengthStat, Target readStatFrom, EntityLinkSchema readStatLink,
            Target applyTo, EntityLinkSchema applyToLink,
            Target ignoreTarget, EntityLinkSchema[] requireLinks, PhysicsTriggerHitMode hitMode)
        {
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
                    Channel = channel,
                    Magnitude = magnitude,
                    Direction = math.normalizesafe(direction, new float3(0, 0, 1)),
                    OriginMode = originMode,
                    FalloffCurve = falloffCurve,
                    FalloffStartRadius = falloffStartRadius,
                    FalloffEndRadius = falloffEndRadius,
                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    },
                    ApplyTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, applyToLink, applyTo),
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
