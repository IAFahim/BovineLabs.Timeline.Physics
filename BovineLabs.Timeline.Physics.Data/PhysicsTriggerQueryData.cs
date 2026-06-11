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

        /// <summary>Smallest angle between the query origin's forward and the candidate.</summary>
        MostAligned,

        /// <summary>Largest angle between the query origin's forward and the candidate.</summary>
        LeastAligned
    }

    /// <summary>
    ///     Per-tick selection over the bound entity's stateful trigger/collision events.
    ///     Candidates pass the shared <see cref="PhysicsTriggerFilterData" />, the collision mask,
    ///     distance, view-angle, and line-of-sight gates; the configured selection policy picks one
    ///     winner deterministically (ties broken by entity index, then version). The winner is
    ///     written into the routed entity's <see cref="Targets.Custom" /> slot — so every other
    ///     track that targets Custom (PID chase, force toward, teleport relative-to, …) composes
    ///     with it — and found/lost transitions raise the configured conditions.
    /// </summary>
    public struct PhysicsTriggerQueryData : IComponentData
    {
        public StatefulEventState EventState;

        /// <summary>Candidate collider must belong to one of these categories. 0 accepts any.</summary>
        public uint CollidesWithMask;

        /// <summary>Maximum query distance. Values &lt;= 0 are unlimited.</summary>
        public float MaxDistance;

        /// <summary>Maximum angle (radians) from the query origin's forward. Values &lt;= 0 disable the gate.</summary>
        public float MaxAngle;

        public bool RequireLineOfSight;
        public uint ObstacleMask;
        public float LineOfSightOffset;

        public PhysicsTriggerQuerySelection Selection;

        /// <summary>Whose <see cref="Targets.Custom" /> receives the winner. The query track owns that slot while active.</summary>
        public Target RouteTo;

        public ushort RouteLinkKey;

        /// <summary>When all candidates are lost, write <see cref="Entity.Null" /> to the routed Custom slot.</summary>
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
