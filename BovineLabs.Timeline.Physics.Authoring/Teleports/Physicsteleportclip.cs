using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Authoring.Conditions;
using BovineLabs.Reaction.Data.Conditions;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTeleportClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Teleport Target")]
        [Tooltip("The entity to actually teleport.")]
        public Target entityToTeleport = Target.Owner;
        public EntityLinkSchema entityToTeleportLink;
        
        [Header("Destination")]
        [Tooltip("Distance from the target entity to teleport to.")]
        public float radius = 3f;

        [Tooltip("Whose position defines the sphere origin.")]
        public Target teleportRelativeTo = Target.Target;

        public EntityLinkSchema teleportRelativeToLink;

        [Header("Spherical Patch (degrees)")]
        [Tooltip("Center azimuth on the reference plane. 0 = reference forward, 180 = behind.")]
        [Range(-180f, 180f)]
        public float azimuthCenter;

        [Tooltip("Half-spread in azimuth. 180 = full ring.")]
        [Range(0f, 180f)]
        public float azimuthHalfRange = 180f;

        [Tooltip("Center elevation. 0 = horizontal plane, 90 = directly above.")]
        [Range(-90f, 90f)]
        public float elevationCenter;

        [Tooltip("Half-spread in elevation. 90 = full hemisphere.")]
        [Range(0f, 90f)]
        public float elevationHalfRange = 30f;

        [Tooltip("How the reference forward direction is derived.")]
        public TeleportReferenceFrame referenceFrame = TeleportReferenceFrame.TargetToSelf;

        [Header("Facing After Teleport")]
        public TeleportFacingMode facingMode = TeleportFacingMode.FaceTarget;

        [Header("Clearance")]
        [Tooltip("Sphere radius for obstacle clearance checks at each candidate.")]
        [Min(0.1f)]
        public float clearanceRadius = 0.5f;

        [Tooltip("Maximum candidate positions to evaluate before giving up.")]
        [Range(1, 64)]
        public int maxCandidates = 16;

        [Tooltip("Physics layers that count as obstacles for clearance and LOS.")]
        public PhysicsCategoryTags obstacleMask;

        [Header("Line of Sight")]
        [Tooltip("Require unobstructed LOS from self to target before teleporting. On failure, fires the failure condition.")]
        public bool requireLineOfSight;

        [Tooltip("Also require each candidate position to have LOS back to the target.")]
        public bool requireCandidateVisibility;

        [Tooltip("Vertical offset applied to ray origins for LOS checks (eye height).")]
        public float lineOfSightOffset = 1.5f;

        [Header("Velocity")]
        [Tooltip("Zero linear and angular velocity after teleport.")]
        public bool resetVelocity = true;

        [Header("Stat Multiplier (Optional)")]
        [Tooltip("If set, multiplies the radius by this stat value.")]
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
            ushort teleportTargetLinkKey = 0;
            if (entityToTeleportLink != null && EntityLinkAuthoringUtility.TryGetKey(entityToTeleportLink, out var k00))
                teleportTargetLinkKey = k00;

            ushort teleportLinkKey = 0;
            if (teleportRelativeToLink != null && EntityLinkAuthoringUtility.TryGetKey(teleportRelativeToLink, out var k0))
                teleportLinkKey = k0;

            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            ushort failureLinkKey = 0;
            if (failureRouteLink != null && EntityLinkAuthoringUtility.TryGetKey(failureRouteLink, out var k2))
                failureLinkKey = k2;

context.Baker.AddComponent(clipEntity, new PhysicsTeleportAnimated
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
                    ReferenceFrame = referenceFrame,
                    FacingMode = facingMode,
                    ClearanceRadius = clearanceRadius,
                    MaxCandidates = maxCandidates,
                    ObstacleMask = obstacleMask.Value,
                    RequireLineOfSight = requireLineOfSight,
                    RequireCandidateVisibility = requireCandidateVisibility,
                    LineOfSightOffset = lineOfSightOffset,
                    ResetVelocity = resetVelocity,
                    TeleportRelativeTo = teleportRelativeTo,
                    TeleportRelativeToLinkKey = teleportLinkKey,
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
            });

            base.Bake(clipEntity, context);
        }
    }
}