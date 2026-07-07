using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
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
        public EntityLinkRef EntityToTeleport;

        public float Radius;

        public float AzimuthCenter;
        public float AzimuthHalfRange;
        public float ElevationCenter;
        public float ElevationHalfRange;

        public EntityLinkRef TeleportRelativeTo;

        public EntityLinkRef AzimuthTarget;

        public TeleportReferenceFrame ReferenceFrame;

        public TeleportFacingMode FacingMode;
        public EntityLinkRef FacingTarget;

        public float ClearanceRadius;
        public int MaxCandidates;
        public uint ObstacleMask;

        public bool RequireLineOfSight;
        public bool RequireCandidateVisibility;
        public float LineOfSightOffset;
        public bool ResetVelocity;

        public ConditionKey FailureCondition;
        public int FailureValue;
        public EntityLinkRef FailureRouteTo;

        public StatSource Strength;
    }

    public struct PhysicsTeleportState : IComponentData, IDrainableLatchState<PhysicsTeleportData>
    {
        public bool Fired;

        /// <summary>
        /// Fixed-step drain gate: set when the render-side stale-disable lingers this latch enabled because it has not
        /// fired yet (a short teleport clip whose enable window straddled no fixed tick), so the fixed clock gets one
        /// apply tick to teleport before the drain-finalize disables it. Mirrors PhysicsForceState.
        /// </summary>
        public bool Orphaned;

        bool IOrphanedLatch.Orphaned
        {
            get => Orphaned;
            set => Orphaned = value;
        }

        // A teleport is a pure one-shot: drained once fired. An unfired latch must linger so a clip that never met a
        // fixed tick still teleports instead of silently doing nothing.
        public bool IsDrained(in PhysicsTeleportData config)
        {
            return Fired;
        }
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

                Radius = math.lerp(a.Radius, b.Radius, s),
                AzimuthCenter = math.lerp(a.AzimuthCenter, b.AzimuthCenter, s),
                AzimuthHalfRange = math.lerp(a.AzimuthHalfRange, b.AzimuthHalfRange, s),
                ElevationCenter = math.lerp(a.ElevationCenter, b.ElevationCenter, s),
                ElevationHalfRange = math.lerp(a.ElevationHalfRange, b.ElevationHalfRange, s),

                TeleportRelativeTo = s < 0.5f ? a.TeleportRelativeTo : b.TeleportRelativeTo,

                AzimuthTarget = s < 0.5f ? a.AzimuthTarget : b.AzimuthTarget,

                ReferenceFrame = s < 0.5f ? a.ReferenceFrame : b.ReferenceFrame,

                FacingMode = s < 0.5f ? a.FacingMode : b.FacingMode,
                FacingTarget = s < 0.5f ? a.FacingTarget : b.FacingTarget,

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

                Strength = s < 0.5f ? a.Strength : b.Strength
            };
        }

        public PhysicsTeleportData Add(in PhysicsTeleportData a, in PhysicsTeleportData b)
        {
            return new PhysicsTeleportData
            {
                EntityToTeleport = a.EntityToTeleport,

                Radius = a.Radius + b.Radius,
                AzimuthCenter = a.AzimuthCenter + b.AzimuthCenter,
                AzimuthHalfRange = a.AzimuthHalfRange + b.AzimuthHalfRange,
                ElevationCenter = a.ElevationCenter + b.ElevationCenter,
                ElevationHalfRange = a.ElevationHalfRange + b.ElevationHalfRange,

                TeleportRelativeTo = a.TeleportRelativeTo,

                AzimuthTarget = a.AzimuthTarget,

                ReferenceFrame = a.ReferenceFrame,

                FacingMode = a.FacingMode,
                FacingTarget = a.FacingTarget,

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

                Strength = a.Strength
            };
        }
    }
}