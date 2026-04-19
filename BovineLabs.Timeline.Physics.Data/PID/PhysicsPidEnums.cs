namespace BovineLabs.Timeline.Physics
{
    public enum PidLinearTargetMode : byte
    {
        TargetLocal,
        LineOfSight,
        World
    }

    public enum PidAngularTargetMode : byte
    {
        MatchTarget,
        LookAtTarget,
        World
    }
}