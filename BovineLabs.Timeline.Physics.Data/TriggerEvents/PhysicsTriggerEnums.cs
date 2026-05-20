using BovineLabs.Core.PhysicsStates;

namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerPositionMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        MatchContactPoint
    }

    public enum PhysicsTriggerRotationMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        AlignToContactNormal,
        Identity
    }

    public enum PhysicsTriggerTargetMode : byte
    {
        Self,
        CollidedEntity,
        ReactionOwner,
        ReactionSource,
        ReactionTarget
    }

    public static class StatefulEventMatching
    {
        public static bool Matches(StatefulEventState actual, StatefulEventState wanted,
            bool isClipFirstFrame, bool isClipLastFrame)
        {
            if (actual == wanted) return true;
            if (actual != StatefulEventState.Stay) return false;
            if (isClipFirstFrame && wanted == StatefulEventState.Enter) return true;
            if (isClipLastFrame && wanted == StatefulEventState.Exit) return true;
            return false;
        }
    }
}