using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Mathematics;
using Unity.Physics.Authoring;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Shared bake ceremony for the focused query clips (TargetSelect / DirectionalQuery / AoE). Each preset fills a
    /// <see cref="PhysicsTriggerQueryData"/> with only its relevant fields; this overlays the fields every query
    /// shares (event/filter/route/conditions/distance) and routes through the same builder the legacy god-clip used.
    /// Exotic gate/selection/value params left at default are inert — the runtime only reads them when their gate
    /// flag / Selection / ValueMode is active, which the focused presets never set.
    /// </summary>
    internal static class PhysicsQueryClipBaking
    {
        internal struct Common
        {
            public StatefulEventState TriggerState;
            public PhysicsCategoryTags CollidesWith;
            public float MaxDistance;
            public float MaxAngleDegrees;
            public Target RouteTo;
            public EntityLinkSchema RouteLink;
            public PhysicsTriggerRouteSlot RouteSlot;
            public PhysicsTriggerWriteMode WriteMode;
            public bool ClearOnLost;
            public int GraceFrames;
            public ConditionEventObject FoundCondition;
            public int FoundValue;
            public ConditionEventObject LostCondition;
            public int LostValue;
            public Target IgnoreTarget;
            public EntityLinkSchema[] RequireLinks;
        }

        public static PhysicsTriggerQueryBuilder Build(BakingContext context, in Common c, PhysicsTriggerQueryData data)
        {
            var filterBlob = PhysicsTriggerBakingUtility.BakeFilterBlob(context.Baker, c.RequireLinks);

            data.EventState = c.TriggerState;
            data.CollidesWithMask = c.CollidesWith.Value;
            data.MaxDistance = c.MaxDistance;
            data.MaxAngle = math.radians(c.MaxAngleDegrees);
            data.RouteTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, c.RouteLink, c.RouteTo);
            data.RouteSlot = c.RouteSlot;
            data.WriteMode = c.WriteMode;
            data.ClearOnLost = c.ClearOnLost;
            data.GraceFrames = (ushort)math.clamp(c.GraceFrames, 0, ushort.MaxValue);
            data.FoundCondition = c.FoundCondition ? new ConditionKey(c.FoundCondition.Key) : ConditionKey.Null;
            data.FoundValue = c.FoundValue;
            data.LostCondition = c.LostCondition ? new ConditionKey(c.LostCondition.Key) : ConditionKey.Null;
            data.LostValue = c.LostValue;

            return new PhysicsTriggerQueryBuilder
            {
                QueryData = data,
                FilterData = new PhysicsTriggerFilterData
                {
                    IgnoreTarget = c.IgnoreTarget,
                    LinkFilterBlob = filterBlob,
                },
            };
        }
    }
}
