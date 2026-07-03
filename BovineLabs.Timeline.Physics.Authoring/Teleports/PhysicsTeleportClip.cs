using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
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
        public EntityLinkSchema entityToTeleportLink;
        public EntityLinkSchema teleportRelativeToLink;
        public EntityLinkSchema azimuthTargetLink;
        public EntityLinkSchema facingTargetLink;
        public EntityLinkSchema readStatLink;
        public EntityLinkSchema failureRouteLink;

        [Header("Teleport Target")] [Tooltip("The entity to actually teleport.")]
        public Target entityToTeleport = Target.Owner;

        [Header("Landing Sphere")] [Tooltip("Distance from the sphere origin to land at.")]
        public float radius = 3f;

        [Tooltip("Whose position defines the sphere origin (landing patch center).")]
        public Target teleportRelativeTo = Target.Self;

        [Header("Landing Direction")]
        [Tooltip(
            "What azimuth 0° points toward. Default (Target) means: azCenter=0 lands in the direction of this entity.")]
        public Target azimuthTarget = Target.Target;

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

        [Header("On Failure")]
        [Tooltip("Condition event fired when teleport fails (no valid position or LOS blocked).")]
        public ConditionEventObject failureCondition;

        public int failureValue = 1;
        public Target failureRouteTo = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var builder = new PhysicsTeleportBuilder
            {
                AuthoredData = new PhysicsTeleportData
                {
                    EntityToTeleport = EntityLinkAuthoringUtility.BakeRef(context.Baker, entityToTeleportLink, entityToTeleport),

                    Radius = radius,

                    AzimuthCenter = math.radians(azimuthCenter),
                    AzimuthHalfRange = math.radians(azimuthHalfRange),
                    ElevationCenter = math.radians(elevationCenter),
                    ElevationHalfRange = math.radians(elevationHalfRange),

                    TeleportRelativeTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, teleportRelativeToLink, teleportRelativeTo),

                    AzimuthTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, azimuthTargetLink, azimuthTarget),

                    ReferenceFrame = referenceFrame,

                    FacingMode = facingMode,
                    FacingTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, facingTargetLink, facingTarget),

                    ClearanceRadius = clearanceRadius,
                    MaxCandidates = maxCandidates,
                    ObstacleMask = obstacleMask.Value,

                    RequireLineOfSight = requireLineOfSight,
                    RequireCandidateVisibility = requireCandidateVisibility,
                    LineOfSightOffset = lineOfSightOffset,
                    ResetVelocity = resetVelocity,

                    FailureCondition = failureCondition ? failureCondition.Key : ConditionKey.Null,
                    FailureValue = failureValue,
                    FailureRouteTo = EntityLinkAuthoringUtility.BakeRef(context.Baker, failureRouteLink, failureRouteTo),

                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}