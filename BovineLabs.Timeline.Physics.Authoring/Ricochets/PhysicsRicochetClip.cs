using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Ricochets
{
    public sealed class PhysicsRicochetClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema hitRouteLink;
        public EntityLinkSchema rayOriginLink;
        public EntityLinkSchema rayDirectionLink;
        public EntityLinkSchema readStatLink;

        public int maxBounces = 3;
        public float maxDistance = 50f;
        [Range(0f, 90f)] public float minGrazingAngle = 15f;
        public PhysicsCategoryTags ricochetSurfaces = PhysicsCategoryTags.Everything;
        public PhysicsCategoryTags terminalHitSurfaces = PhysicsCategoryTags.Everything;

        [Header("Terminal Hit Event")] public ConditionEventObject hitCondition;

        public Target hitRouteTo = Target.Target;

        [Header("Ray Origin")] public Target rayOrigin = Target.Self;

        [Header("Ray Direction")] public Target rayDirection = Target.Self;

        [Header("Distance Multiplier (Optional)")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var builder = new PhysicsRicochetBuilder
            {
                AuthoredData = new PhysicsRicochetData
                {
                    MaxBounces = maxBounces,
                    MaxDistance = maxDistance,
                    MinGrazingAngle = math.radians(minGrazingAngle),
                    RicochetMask = ricochetSurfaces.Value,
                    TerminalHitMask = terminalHitSurfaces.Value,
                    HitConditionKey = hitCondition != null ? new ConditionKey(hitCondition.Key) : ConditionKey.Null,
                    HitRouteTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, hitRouteLink, hitRouteTo),
                    RayOrigin = EntityLinkAuthoringUtility.BakeRef(context.Baker, rayOriginLink, rayOrigin),
                    RayDirection = EntityLinkAuthoringUtility.BakeRef(context.Baker, rayDirectionLink, rayDirection),
                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}