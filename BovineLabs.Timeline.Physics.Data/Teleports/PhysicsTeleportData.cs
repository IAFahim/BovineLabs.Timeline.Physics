using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public enum TeleportReferenceFrame : byte
    {
        TargetToSelf,
        SelfToTarget,
        TargetForward,
        WorldForward
    }

    public enum TeleportFacingMode : byte
    {
        FaceTarget,
        FaceAway,
        PreserveCurrent,
        MatchTarget
    }

    public struct PhysicsTeleportData
    {
        public Target EntityToTeleport;
        public ushort EntityToTeleportLinkKey;

        public float Radius;

        public float AzimuthCenter;
        public float AzimuthHalfRange;
        public float ElevationCenter;
        public float ElevationHalfRange;

        public Target TeleportRelativeTo;
        public ushort TeleportRelativeToLinkKey;

        public Target AzimuthTarget;
        public ushort AzimuthTargetLinkKey;

        public TeleportReferenceFrame ReferenceFrame;

        public TeleportFacingMode FacingMode;
        public Target FacingTarget;
        public ushort FacingTargetLinkKey;

        public float ClearanceRadius;
        public int MaxCandidates;
        public uint ObstacleMask;

        public bool RequireLineOfSight;
        public bool RequireCandidateVisibility;
        public float LineOfSightOffset;
        public bool ResetVelocity;

        public ConditionKey FailureCondition;
        public int FailureValue;
        public Target FailureRouteTo;
        public ushort FailureRouteLinkKey;

        public StatStrengthConfig Strength;
    }

    public struct PhysicsTeleportState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsTeleportAnimated : IAnimatedComponent<PhysicsTeleportData>, IPreparable
    {
        public PhysicsTeleportData AuthoredData;
        [CreateProperty] public PhysicsTeleportData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveTeleport : IActive<PhysicsTeleportData>
    {
        public PhysicsTeleportData Config { get; set; }
    }

    public readonly struct PhysicsTeleportMixer : IMixer<PhysicsTeleportData>
    {
        public PhysicsTeleportData Lerp(in PhysicsTeleportData a, in PhysicsTeleportData b, in float s)
        {
            return new PhysicsTeleportData
            {
                EntityToTeleport = s < 0.5f ? a.EntityToTeleport : b.EntityToTeleport,
                EntityToTeleportLinkKey = s < 0.5f ? a.EntityToTeleportLinkKey : b.EntityToTeleportLinkKey,

                Radius = math.lerp(a.Radius, b.Radius, s),
                AzimuthCenter = math.lerp(a.AzimuthCenter, b.AzimuthCenter, s),
                AzimuthHalfRange = math.lerp(a.AzimuthHalfRange, b.AzimuthHalfRange, s),
                ElevationCenter = math.lerp(a.ElevationCenter, b.ElevationCenter, s),
                ElevationHalfRange = math.lerp(a.ElevationHalfRange, b.ElevationHalfRange, s),

                TeleportRelativeTo = s < 0.5f ? a.TeleportRelativeTo : b.TeleportRelativeTo,
                TeleportRelativeToLinkKey = s < 0.5f ? a.TeleportRelativeToLinkKey : b.TeleportRelativeToLinkKey,

                AzimuthTarget = s < 0.5f ? a.AzimuthTarget : b.AzimuthTarget,
                AzimuthTargetLinkKey = s < 0.5f ? a.AzimuthTargetLinkKey : b.AzimuthTargetLinkKey,

                ReferenceFrame = s < 0.5f ? a.ReferenceFrame : b.ReferenceFrame,

                FacingMode = s < 0.5f ? a.FacingMode : b.FacingMode,
                FacingTarget = s < 0.5f ? a.FacingTarget : b.FacingTarget,
                FacingTargetLinkKey = s < 0.5f ? a.FacingTargetLinkKey : b.FacingTargetLinkKey,

                ClearanceRadius = math.lerp(a.ClearanceRadius, b.ClearanceRadius, s),
                MaxCandidates = s < 0.5f ? a.MaxCandidates : b.MaxCandidates,
                ObstacleMask = s < 0.5f ? a.ObstacleMask : b.ObstacleMask,

                RequireLineOfSight = s < 0.5f ? a.RequireLineOfSight : b.RequireLineOfSight,
                RequireCandidateVisibility = s < 0.5f ? a.RequireCandidateVisibility : b.RequireCandidateVisibility,
                LineOfSightOffset = math.lerp(a.LineOfSightOffset, b.LineOfSightOffset, s),
                ResetVelocity = s < 0.5f ? a.ResetVelocity : b.ResetVelocity,

                FailureCondition = s < 0.5f ? a.FailureCondition : b.FailureCondition,
                FailureValue = s < 0.5f ? a.FailureValue : b.FailureValue,
                FailureRouteTo = s < 0.5f ? a.FailureRouteTo : b.FailureRouteTo,
                FailureRouteLinkKey = s < 0.5f ? a.FailureRouteLinkKey : b.FailureRouteLinkKey,

                Strength = s < 0.5f ? a.Strength : b.Strength
            };
        }

        public PhysicsTeleportData Add(in PhysicsTeleportData a, in PhysicsTeleportData b)
        {
            return new PhysicsTeleportData
            {
                EntityToTeleport = a.EntityToTeleport,
                EntityToTeleportLinkKey = a.EntityToTeleportLinkKey,

                Radius = a.Radius + b.Radius,
                AzimuthCenter = a.AzimuthCenter + b.AzimuthCenter,
                AzimuthHalfRange = a.AzimuthHalfRange + b.AzimuthHalfRange,
                ElevationCenter = a.ElevationCenter + b.ElevationCenter,
                ElevationHalfRange = a.ElevationHalfRange + b.ElevationHalfRange,

                TeleportRelativeTo = a.TeleportRelativeTo,
                TeleportRelativeToLinkKey = a.TeleportRelativeToLinkKey,

                AzimuthTarget = a.AzimuthTarget,
                AzimuthTargetLinkKey = a.AzimuthTargetLinkKey,

                ReferenceFrame = a.ReferenceFrame,

                FacingMode = a.FacingMode,
                FacingTarget = a.FacingTarget,
                FacingTargetLinkKey = a.FacingTargetLinkKey,

                ClearanceRadius = a.ClearanceRadius + b.ClearanceRadius,
                MaxCandidates = a.MaxCandidates,
                ObstacleMask = a.ObstacleMask,

                RequireLineOfSight = a.RequireLineOfSight,
                RequireCandidateVisibility = a.RequireCandidateVisibility,
                LineOfSightOffset = a.LineOfSightOffset + b.LineOfSightOffset,
                ResetVelocity = a.ResetVelocity,

                FailureCondition = a.FailureCondition,
                FailureValue = a.FailureValue,
                FailureRouteTo = a.FailureRouteTo,
                FailureRouteLinkKey = a.FailureRouteLinkKey,

                Strength = a.Strength
            };
        }
    }
}