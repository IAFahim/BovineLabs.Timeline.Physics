using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Teleports
{
    public sealed class PhysicsTeleportClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Teleport Target")] [Tooltip("The entity to actually teleport.")]
        public Target entityToTeleport = Target.Owner;

        public EntityLinkSchema entityToTeleportLink;

        [Header("Landing Sphere")] [Tooltip("Distance from the sphere origin to land at.")]
        public float radius = 3f;

        [Tooltip("Whose position defines the sphere origin (landing patch center).")]
        public Target teleportRelativeTo = Target.Self;

        public EntityLinkSchema teleportRelativeToLink;

        [Header("Landing Direction")]
        [Tooltip(
            "What azimuth 0° points toward. Default (Target) means: azCenter=0 lands in the direction of this entity.")]
        public Target azimuthTarget = Target.Target;

        public EntityLinkSchema azimuthTargetLink;

        [Tooltip(
            "Frame defining azimuth 0° around the landing patch origin. SelfToTarget: 0° points toward the azimuth target. " +
            "TargetToSelf: 0° points away from it. TargetForward: the azimuth target's own forward. WorldForward: world +Z.")]
        public TeleportReferenceFrame referenceFrame = TeleportReferenceFrame.SelfToTarget;

        [Header("Azimuth / Elevation (degrees)")]
        [Tooltip(
            "Center azimuth. 0 = toward azimuth target, 90 = 90° clockwise from it, 180 = opposite (away from azimuth target).")]
        [Range(-180f, 180f)]
        public float azimuthCenter;

        [Tooltip("Half-spread in azimuth. 180 = full ring around the azimuth target.")] [Range(0f, 180f)]
        public float azimuthHalfRange = 180f;

        [Tooltip("Center elevation. 0 = horizontal plane, 90 = directly above, -90 = directly below.")]
        [Range(-90f, 90f)]
        public float elevationCenter;

        [Tooltip("Half-spread in elevation. 90 = full hemisphere.")] [Range(0f, 90f)]
        public float elevationHalfRange = 30f;

        [Header("Facing After Teleport")] [Tooltip("How the entity orients after landing.")]
        public TeleportFacingMode facingMode = TeleportFacingMode.FaceTarget;

        [Tooltip("Which entity it faces. Separate from Landing Direction so these can differ.")]
        public Target facingTarget = Target.Target;

        public EntityLinkSchema facingTargetLink;

        [Header("Clearance")] [Tooltip("Sphere radius for obstacle clearance checks at each candidate.")] [Min(0.1f)]
        public float clearanceRadius = 0.5f;

        [Tooltip("Maximum candidate positions to evaluate before giving up.")] [Range(1, 64)]
        public int maxCandidates = 8;

        [Tooltip("Physics layers that count as obstacles for clearance and LOS.")]
        public PhysicsCategoryTags obstacleMask;

        [Header("Line of Sight")]
        [Tooltip(
            "Require unobstructed LOS from the teleported entity to the landing sphere origin before teleporting.")]
        public bool requireLineOfSight;

        [Tooltip("Also require each candidate position to have LOS back to the landing sphere origin.")]
        public bool requireCandidateVisibility;

        [Tooltip("Vertical offset applied to ray origins for LOS checks (eye height).")]
        public float lineOfSightOffset = 1.5f;

        [Header("Velocity")] [Tooltip("Zero linear and angular velocity after teleport.")]
        public bool resetVelocity = true;

        [Header("Stat Multiplier (Optional)")] [Tooltip("If set, multiplies the radius by this stat value.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        [Header("On Failure")]
        [Tooltip("Condition event fired when teleport fails (no valid position or LOS blocked).")]
        public ConditionEventObject failureCondition;

        public int failureValue = 1;
        public Target failureRouteTo = Target.Self;
        public EntityLinkSchema failureRouteLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            ushort teleportTargetLinkKey = 0;
            if (entityToTeleportLink != null && EntityLinkAuthoringUtility.TryGetKey(entityToTeleportLink, out var k00))
                teleportTargetLinkKey = k00;

            ushort teleportLinkKey = 0;
            if (teleportRelativeToLink != null &&
                EntityLinkAuthoringUtility.TryGetKey(teleportRelativeToLink, out var k0))
                teleportLinkKey = k0;

            ushort azimuthTargetLinkKey = 0;
            if (azimuthTargetLink != null && EntityLinkAuthoringUtility.TryGetKey(azimuthTargetLink, out var kAz))
                azimuthTargetLinkKey = kAz;

            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            ushort failureLinkKey = 0;
            if (failureRouteLink != null && EntityLinkAuthoringUtility.TryGetKey(failureRouteLink, out var k2))
                failureLinkKey = k2;

            ushort facingTargetLinkKey = 0;
            if (facingTargetLink != null && EntityLinkAuthoringUtility.TryGetKey(facingTargetLink, out var k3))
                facingTargetLinkKey = k3;

            var builder = new PhysicsTeleportBuilder
            {
                AuthoredData = new PhysicsTeleportData
                {
                    EntityToTeleport = entityToTeleport,
                    EntityToTeleportLinkKey = teleportTargetLinkKey,

                    Radius = radius,

                    AzimuthCenter = math.radians(azimuthCenter),
                    AzimuthHalfRange = math.radians(azimuthHalfRange),
                    ElevationCenter = math.radians(elevationCenter),
                    ElevationHalfRange = math.radians(elevationHalfRange),

                    TeleportRelativeTo = teleportRelativeTo,
                    TeleportRelativeToLinkKey = teleportLinkKey,

                    AzimuthTarget = azimuthTarget,
                    AzimuthTargetLinkKey = azimuthTargetLinkKey,

                    ReferenceFrame = referenceFrame,

                    FacingMode = facingMode,
                    FacingTarget = facingTarget,
                    FacingTargetLinkKey = facingTargetLinkKey,

                    ClearanceRadius = clearanceRadius,
                    MaxCandidates = maxCandidates,
                    ObstacleMask = obstacleMask.Value,

                    RequireLineOfSight = requireLineOfSight,
                    RequireCandidateVisibility = requireCandidateVisibility,
                    LineOfSightOffset = lineOfSightOffset,
                    ResetVelocity = resetVelocity,

                    FailureCondition = failureCondition ? failureCondition.Key : ConditionKey.Null,
                    FailureValue = failureValue,
                    FailureRouteTo = failureRouteTo,
                    FailureRouteLinkKey = failureLinkKey,

                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}