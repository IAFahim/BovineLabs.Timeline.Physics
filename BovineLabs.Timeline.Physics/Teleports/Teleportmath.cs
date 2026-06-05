namespace BovineLabs.Timeline.Physics.Teleports
{

    using BovineLabs.Core.Iterators;
    using BovineLabs.Reaction.Data.Core;
    using EntityLinks;
    using BovineLabs.Timeline.EntityLinks.Data;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;

    public static class TeleportMath
    {
        private const float GoldenAngle = 2.39996322972865332f;
        private const float ElevationClamp = math.PI * 0.5f - 0.01f;

        /// <summary>
        /// Computes the reference rotation used for azimuth/elevation candidate generation.
        /// The forward direction of the returned quaternion defines what azimuth 0° points toward.
        /// </summary>
        /// <param name="selfPos">Position of the entity being teleported.</param>
        /// <param name="selfRot">Rotation of the entity being teleported.</param>
        /// <param name="azimuthTargetPos">Position that defines the azimuth reference direction.</param>
        /// <param name="azimuthTargetRot">Rotation of the azimuth target entity.</param>
        /// <param name="referenceFrame">Legacy frame selector (kept for compatibility, prefer direct pos/rot).</param>
        /// <param name="referenceRotation">Output: quaternion where forward = azimuth 0°.</param>
        public static void ResolveReferenceRotation(
            float3 selfPos,
            quaternion selfRot,
            float3 azimuthTargetPos,
            quaternion azimuthTargetRot,
            TeleportReferenceFrame referenceFrame,
            out quaternion referenceRotation)
        {
            // Compute the direction from azimuth target to self (what "self is relative to the target").
            var toSelf = selfPos - azimuthTargetPos;
            var lenSq = math.lengthsq(toSelf);

            if (lenSq < 1e-6f)
            {
                // Fallback: use the azimuth target's rotation as the reference.
                referenceRotation = azimuthTargetRot;
                return;
            }

            var toSelfDir = toSelf * math.rsqrt(lenSq);

            // Build the reference rotation where:
            // - forward points toward azimuthTargetPos
            // - up is world up
            referenceRotation = quaternion.LookRotationSafe(toSelfDir, math.up());
        }

        public static void GenerateCandidate(
            int index,
            int totalCandidates,
            float azimuthCenter,
            float azimuthHalfRange,
            float elevationCenter,
            float elevationHalfRange,
            quaternion referenceRotation,
            float radius,
            float3 origin,
            out float3 candidatePosition)
        {
            float azOffset;
            float elOffset;

            if (index == 0 || totalCandidates <= 1)
            {
                azOffset = 0f;
                elOffset = 0f;
            }
            else
            {
                var t = (float)index / (totalCandidates - 1);
                var r = math.sqrt(t);
                var theta = index * GoldenAngle;

                azOffset = r * math.cos(theta) * azimuthHalfRange;
                elOffset = r * math.sin(theta) * elevationHalfRange;
            }

            var az = azimuthCenter + azOffset;
            var el = math.clamp(elevationCenter + elOffset, -ElevationClamp, ElevationClamp);

            var cosEl = math.cos(el);
            var localDir = new float3(cosEl * math.sin(az), math.sin(el), cosEl * math.cos(az));
            var worldDir = math.rotate(referenceRotation, localDir);

            candidatePosition = origin + worldDir * radius;
        }

        public static void ComputeFacingRotation(
            TeleportFacingMode mode,
            float3 teleportedPosition,
            float3 targetPosition,
            quaternion currentRotation,
            quaternion targetRotation,
            out quaternion facingRotation)
        {
            switch (mode)
            {
                case TeleportFacingMode.FaceTarget:
                {
                    var dir = targetPosition - teleportedPosition;
                    if (math.lengthsq(dir) > 1e-5f)
                        facingRotation = quaternion.LookRotationSafe(math.normalize(dir), math.up());
                    else
                        facingRotation = currentRotation;
                    return;
                }
                case TeleportFacingMode.FaceAway:
                {
                    var dir = teleportedPosition - targetPosition;
                    if (math.lengthsq(dir) > 1e-5f)
                        facingRotation = quaternion.LookRotationSafe(math.normalize(dir), math.up());
                    else
                        facingRotation = currentRotation;
                    return;
                }
                case TeleportFacingMode.MatchTarget:
                    facingRotation = targetRotation;
                    return;
                default:
                    facingRotation = currentRotation;
                    return;
            }
        }

        public static bool CheckClearance(
            in CollisionWorld collisionWorld,
            float3 position,
            float clearanceRadius,
            uint obstacleMask,
            Entity selfEntity)
        {
            var input = new PointDistanceInput
            {
                Position = position,
                MaxDistance = clearanceRadius,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = obstacleMask,
                    GroupIndex = 0
                }
            };

            var collector = new IgnoreEntityDistanceCollector(selfEntity, clearanceRadius);
            collisionWorld.CalculateDistance(input, ref collector);
            return !collector.ObstacleDetected;
        }

        public static bool CheckLineOfSight(
            in CollisionWorld collisionWorld,
            float3 fromPosition,
            float3 toPosition,
            float verticalOffset,
            uint obstacleMask,
            Entity ignoreEntityA,
            Entity ignoreEntityB)
        {
            var start = fromPosition + new float3(0f, verticalOffset, 0f);
            var end = toPosition + new float3(0f, verticalOffset, 0f);

            var input = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = obstacleMask,
                    GroupIndex = 0
                }
            };

            var collector = new IgnoreTwoEntitiesRayCollector(ignoreEntityA, ignoreEntityB);
            collisionWorld.CastRay(input, ref collector);
            return !collector.ObstacleDetected;
        }

        public static Entity ResolveTargetEntity(
            Entity selfEntity,
            Target targetMode,
            ushort linkKey,
            in UnsafeComponentLookup<Targets> targetsLookup,
            in UnsafeComponentLookup<EntityLinkSource> linkSources,
            in UnsafeBufferLookup<EntityLinkEntry> linkEntries)
        {
            if (targetMode == Target.None)
                return selfEntity;

            if (!targetsLookup.TryGetComponent(selfEntity, out var targets))
                return selfEntity;

            var baseTarget = targets.Get(targetMode, selfEntity);
            if (baseTarget == Entity.Null)
                return selfEntity;

            if (linkKey == 0)
                return baseTarget;

            if (EntityLinkResolver.TryResolve(baseTarget, linkKey, linkSources, linkEntries, out var linked))
                return linked;

            return baseTarget;
        }

        private struct IgnoreEntityDistanceCollector : ICollector<DistanceHit>
        {
            private readonly Entity _ignore;
            public bool ObstacleDetected;

            public bool EarlyOutOnFirstHit => true;
            public float MaxFraction { get; }
            public int NumHits => ObstacleDetected ? 1 : 0;

            public IgnoreEntityDistanceCollector(Entity ignore, float maxDistance)
            {
                _ignore = ignore;
                ObstacleDetected = false;
                MaxFraction = maxDistance;
            }

            public bool AddHit(DistanceHit hit)
            {
                if (hit.Entity == _ignore) return false;
                ObstacleDetected = true;
                return true;
            }
        }

        private struct IgnoreTwoEntitiesRayCollector : ICollector<RaycastHit>
        {
            private readonly Entity _ignoreA;
            private readonly Entity _ignoreB;
            public bool ObstacleDetected;

            public bool EarlyOutOnFirstHit => true;
            public float MaxFraction => 1f;
            public int NumHits => ObstacleDetected ? 1 : 0;

            public IgnoreTwoEntitiesRayCollector(Entity a, Entity b)
            {
                _ignoreA = a;
                _ignoreB = b;
                ObstacleDetected = false;
            }

            public bool AddHit(RaycastHit hit)
            {
                if (hit.Entity == _ignoreA || hit.Entity == _ignoreB) return false;
                ObstacleDetected = true;
                return true;
            }
        }
    }
}