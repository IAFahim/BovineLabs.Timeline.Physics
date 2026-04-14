namespace BovineLabs.Timeline.Physics
{
    public enum InstantiatePositionMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        MatchContactPoint
    }

    public enum InstantiateRotationMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        AlignToContactNormal,
        Identity
    }

    public enum InstantiateParentMode : byte
    {
        None,
        ParentToCollidedEntity,
        ParentToReactionOwner,
        ParentToReactionSource,
        ParentToReactionTarget
    }
}