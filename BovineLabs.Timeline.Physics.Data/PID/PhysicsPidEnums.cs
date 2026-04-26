namespace BovineLabs.Timeline.Physics
{
    public enum PidLinearTargetMode : byte
    {
        TargetLocal, // offset in target's local space — goal moves WITH the tracked entity
        InitialLocal, // same as TargetLocal but captured ONCE at clip start → fixed world point
        LineOfSight, // offset along the line-of-sight to the target
        World // absolute world-space position
    }

    public enum PidAngularTargetMode : byte
    {
        MatchTarget, // copy target rotation + optional offset
        LookAtTarget, // face the target + optional offset
        World // absolute world-space rotation
    }
}