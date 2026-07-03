using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerConditionData : IComponentData
    {
        public StatefulEventState EventState;
        public uint CollidesWithMask;
        public ConditionKey Condition;
        public int Value;
        public EntityLinkRef RouteTo;
    }
}