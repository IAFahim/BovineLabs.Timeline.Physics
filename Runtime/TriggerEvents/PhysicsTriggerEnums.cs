namespace BovineLabs.Timeline.Physics
{
    public enum PhysicsTriggerPositionMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        MatchContactPoint // Fallbacks to midpoint if using Triggers instead of Collisions
    }

    public enum PhysicsTriggerRotationMode : byte
    {
        MatchSelf,
        MatchCollidedEntity,
        AlignToContactNormal, // Fallbacks to LookAt(Collided) if using Triggers
        Identity
    }

    // Used to identify WHO to Parent to (Instantiate) or WHO to Teleport
    public enum PhysicsTriggerTargetMode : byte
    {
        Self,
        CollidedEntity,
        ReactionOwner,
        ReactionSource,
        ReactionTarget
    }
}