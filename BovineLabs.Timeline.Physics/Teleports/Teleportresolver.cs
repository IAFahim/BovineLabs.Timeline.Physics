namespace BovineLabs.Timeline.Physics.Teleports
{

    using BovineLabs.Core.Iterators;
    using BovineLabs.Reaction.Data.Core;
    using BovineLabs.Timeline.EntityLinks.Data;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Transforms;

    public static class TeleportResolver
    {
        public static TeleportFrame Resolve(
            in Entity self,
            in PhysicsTeleportData data,
            float3 fallbackPosition,
            in UnsafeComponentLookup<LocalToWorld> transforms,
            in UnsafeComponentLookup<Targets> targets,
            in UnsafeComponentLookup<EntityLinkSource> linkSources,
            in UnsafeBufferLookup<EntityLinkEntry> links)
        {
            var teleportedEntity = TeleportMath.ResolveTargetEntity(
                self, data.EntityToTeleport, data.EntityToTeleportLinkKey, targets, linkSources, links);

            LocalToWorld teleportedLtw = default;
            var hasTeleported = teleportedEntity != Entity.Null &&
                                transforms.TryGetComponent(teleportedEntity, out teleportedLtw);

            var teleportedPosition = hasTeleported ? teleportedLtw.Position : fallbackPosition;
            var teleportedRotation = hasTeleported ? Orientation(teleportedLtw) : quaternion.identity;

            var landingEntity = TeleportMath.ResolveTargetEntity(
                self, data.TeleportRelativeTo, data.TeleportRelativeToLinkKey, targets, linkSources, links);

            var landingPosition = transforms.TryGetComponent(landingEntity, out var landingLtw)
                ? landingLtw.Position
                : teleportedPosition;

            var azimuthEntity = TeleportMath.ResolveTargetEntity(
                self, data.AzimuthTarget, data.AzimuthTargetLinkKey, targets, linkSources, links);

            var hasAzimuth = transforms.TryGetComponent(azimuthEntity, out var azimuthLtw);
            var azimuthPosition = hasAzimuth ? azimuthLtw.Position : landingPosition;
            var azimuthRotation = hasAzimuth ? Orientation(azimuthLtw) : quaternion.identity;

            var facingEntity = TeleportMath.ResolveTargetEntity(
                self, data.FacingTarget, data.FacingTargetLinkKey, targets, linkSources, links);

            LocalToWorld facingLtw = default;
            var hasFacing = facingEntity != Entity.Null && 
                            transforms.TryGetComponent(facingEntity, out facingLtw);
            
            var facingPosition = hasFacing ? facingLtw.Position : landingPosition;
            var facingRotation = hasFacing ? Orientation(facingLtw) : azimuthRotation;

            TeleportMath.ResolveReferenceRotation(
                teleportedPosition, teleportedRotation, azimuthPosition, azimuthRotation,
                TeleportReferenceFrame.TargetToSelf, out var referenceRotation);

            return new TeleportFrame(
                hasTeleported, teleportedEntity, teleportedPosition, teleportedRotation,
                landingEntity, landingPosition,
                azimuthPosition, azimuthRotation,
                facingPosition, facingRotation,
                referenceRotation);
        }

        private static quaternion Orientation(in LocalToWorld ltw)
        {
            return new quaternion(math.orthonormalize(new float3x3(ltw.Value)));
        }
    }
}