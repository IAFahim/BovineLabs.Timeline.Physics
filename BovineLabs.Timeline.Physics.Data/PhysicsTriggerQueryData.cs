using BovineLabs.Core.PhysicsStates;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerQuerySelection : byte
    {
        Nearest,
        Farthest,

        MostAligned,

        LeastAligned
    }

    public struct PhysicsTriggerQueryData : IComponentData
    {
        public StatefulEventState EventState;

        public uint CollidesWithMask;

        public float MaxDistance;

        public float MaxAngle;

        public bool RequireLineOfSight;
        public uint ObstacleMask;
        public float LineOfSightOffset;

        public PhysicsTriggerQuerySelection Selection;

        public Target RouteTo;

        public ushort RouteLinkKey;

        public bool ClearOnLost;

        public ConditionKey FoundCondition;
        public int FoundValue;
        public ConditionKey LostCondition;
        public int LostValue;
    }

    public struct PhysicsTriggerQueryState : IComponentData
    {
        public Entity LastWinner;
    }
}