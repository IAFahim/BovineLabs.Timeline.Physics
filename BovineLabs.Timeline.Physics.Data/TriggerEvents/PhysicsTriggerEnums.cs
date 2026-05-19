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
}