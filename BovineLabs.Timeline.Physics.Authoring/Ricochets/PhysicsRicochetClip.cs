namespace BovineLabs.Timeline.Physics.Authoring.Ricochets
{
    using BovineLabs.Essence.Authoring;
    using BovineLabs.Reaction.Authoring.Conditions;
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Timeline.Authoring;
    using BovineLabs.Timeline.EntityLinks.Authoring;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics.Authoring;
    using UnityEngine;
    using UnityEngine.Timeline;

    public sealed class PhysicsRicochetClip : DOTSClip, ITimelineClipAsset
    {
        public int maxBounces = 3;
        public float maxDistance = 50f;
        [Range(0f, 90f)] public float minGrazingAngle = 15f;
        public PhysicsCategoryTags ricochetSurfaces = PhysicsCategoryTags.Everything;
        public PhysicsCategoryTags terminalHitSurfaces = PhysicsCategoryTags.Everything;

        [Header("Terminal Hit Event")]
        public ConditionEventObject hitCondition;
        public Target hitRouteTo = Target.Target;
        public EntityLinkSchema hitRouteLink;

        [Header("Ray Origin")]
        public Target rayOrigin = Target.Self;
        public EntityLinkSchema rayOriginLink;
        
        [Header("Ray Direction")]
        public Target rayDirection = Target.Self;
        public EntityLinkSchema rayDirectionLink;

        [Header("Distance Multiplier (Optional)")]
        public StatSchemaObject strengthStat;
        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            ushort hitRouteKey = 0;
            if (hitRouteLink != null && EntityLinkAuthoringUtility.TryGetKey(hitRouteLink, out var k1))
                hitRouteKey = k1;
                
            ushort rayOriginKey = 0;
            if (rayOriginLink != null && EntityLinkAuthoringUtility.TryGetKey(rayOriginLink, out var k2))
                rayOriginKey = k2;
                
            ushort rayDirectionKey = 0;
            if (rayDirectionLink != null && EntityLinkAuthoringUtility.TryGetKey(rayDirectionLink, out var k3))
                rayDirectionKey = k3;
                
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k4))
                readStatKey = k4;

            context.Baker.AddComponent(clipEntity, new PhysicsRicochetAnimated
            {
                AuthoredData = new PhysicsRicochetData
                {
                    MaxBounces = maxBounces,
                    MaxDistance = maxDistance,
                    MinGrazingAngle = math.radians(minGrazingAngle),
                    RicochetMask = ricochetSurfaces.Value,
                    TerminalHitMask = terminalHitSurfaces.Value,
                    HitConditionKey = hitCondition != null ? hitCondition.Key : (ushort)0,
                    HitRouteTo = hitRouteTo,
                    HitRouteLinkKey = hitRouteKey,
                    RayOrigin = rayOrigin,
                    RayOriginLinkKey = rayOriginKey,
                    RayDirection = rayDirection,
                    RayDirectionLinkKey = rayDirectionKey,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            });
            
            base.Bake(clipEntity, context);
        }
    }
}
