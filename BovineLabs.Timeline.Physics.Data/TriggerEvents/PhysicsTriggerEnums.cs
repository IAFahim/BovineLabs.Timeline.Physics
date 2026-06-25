using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Data.Schedular;
using Unity.IntegerTime;

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

        /// <summary>
        /// Crossing-aware test of whether the clip is active over the timer's [previous, current] interval, in the
        /// same inclusive local-time space core uses (<see cref="TimeTransform.IsLocalTimeBounded"/>), but as an
        /// interval overlap rather than a point sample. This is what makes a clip window that is entirely stepped
        /// over in one (low-FPS) timeline update still register as active, instead of being silently skipped.
        /// </summary>
        public static bool IsClipActiveCrossing(in TimerData timer, in TimeTransform timeTransform)
        {
            // length is the clip duration in local time (respects Scale/ClipIn just like ToLocalTimeUnbound).
            var length = (timeTransform.End - timeTransform.Start) * timeTransform.Scale;

            // Degenerate (zero/negative-duration, or Scale 0) clip: the [0, length] window collapses to a point, so
            // the interval test would spuriously latch onto any crossing of local 0. An instantaneous clip isn't a
            // window — leave it to core's point sample (coreActive), matching core's near-never-active behaviour.
            if (length <= DiscreteTime.Zero)
            {
                return false;
            }

            var a = timeTransform.ToLocalTimeUnbound(timer.Time - timer.DeltaTime);
            var b = timeTransform.ToLocalTimeUnbound(timer.Time);
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;

            // overlap of [lo, hi] with the bounded local window [0, length] (inclusive, matching core).
            return hi >= DiscreteTime.Zero && lo <= length;
        }
    }
}