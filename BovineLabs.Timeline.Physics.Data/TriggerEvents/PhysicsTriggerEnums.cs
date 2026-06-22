using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;

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

    public enum PhysicsTriggerHitMode : byte
    {
        AllContacts,
        FirstPerRoot
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

        public static bool IsClipLastFrame(in TimerData timer, in TimeTransform timeTransform)
        {
            if (timer.DeltaTime.Value == 0) return false;

            var previousTime = timer.Time - timer.DeltaTime;
            return timer.DeltaTime.Value < 0
                ? timer.Time <= timeTransform.Start && previousTime > timeTransform.Start
                : timer.Time >= timeTransform.End && previousTime < timeTransform.End;
        }
    }
}