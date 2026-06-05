using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public readonly struct TeleportFrame
    {
        public readonly bool HasTeleportedEntity;
        public readonly Entity TeleportedEntity;
        public readonly float3 TeleportedPosition;
        public readonly quaternion TeleportedRotation;

        public readonly Entity LandingEntity;
        public readonly float3 LandingPosition;

        public readonly float3 AzimuthPosition;
        public readonly quaternion AzimuthRotation;

        public readonly float3 FacingPosition;
        public readonly quaternion FacingRotation;

        public readonly quaternion ReferenceRotation;

        public TeleportFrame(
            bool hasTeleportedEntity,
            Entity teleportedEntity,
            float3 teleportedPosition,
            quaternion teleportedRotation,
            Entity landingEntity,
            float3 landingPosition,
            float3 azimuthPosition,
            quaternion azimuthRotation,
            float3 facingPosition,
            quaternion facingRotation,
            quaternion referenceRotation)
        {
            HasTeleportedEntity = hasTeleportedEntity;
            TeleportedEntity = teleportedEntity;
            TeleportedPosition = teleportedPosition;
            TeleportedRotation = teleportedRotation;
            LandingEntity = landingEntity;
            LandingPosition = landingPosition;
            AzimuthPosition = azimuthPosition;
            AzimuthRotation = azimuthRotation;
            FacingPosition = facingPosition;
            FacingRotation = facingRotation;
            ReferenceRotation = referenceRotation;
        }
    }
}