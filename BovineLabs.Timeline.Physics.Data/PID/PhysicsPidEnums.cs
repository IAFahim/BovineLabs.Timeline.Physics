namespace BovineLabs.Timeline.Physics
{
    public enum PidLinearTargetMode : byte
    {
        TargetLocal, // offset in target's local space — goal moves WITH the tracked entity
        InitialLocal, // same as TargetLocal but captured ONCE at clip start → fixed world point
        LineOfSight, // offset along the line-of-sight to the target
        World, // absolute world-space position
        FleeFromTarget // goal = self + (self - target): reflected point, PID pushes away
    }

    public enum PidAngularTargetMode : byte
    {
        MatchTarget, // copy target rotation + optional offset
        LookAtTarget, // face the target + optional offset
        World, // absolute world-space rotation
        FleeFromTarget, // face directly away from target
        MatchTargetOpposite // copy target rotation but yaw-flipped 180°
    }
}