namespace BovineLabs.Timeline.Physics
{
    public enum PidLinearTargetMode : byte
    {
        TargetLocal,
        InitialLocal,
        LineOfSight,
        World,
        FleeFromTarget
    }

    public enum PidAngularTargetMode : byte
    {
        MatchTarget,
        LookAtTarget,
        World,
        FleeFromTarget,
        MatchTargetOpposite
    }
}