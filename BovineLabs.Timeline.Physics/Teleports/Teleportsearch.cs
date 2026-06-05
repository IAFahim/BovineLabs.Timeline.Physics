using Unity.Mathematics;
using Unity.Physics;

namespace BovineLabs.Timeline.Physics
{
    public static class TeleportSearch
    {
        private const int MinCandidates = 1;
        private const int MaxCandidates = 64;

        public static bool TryFindLanding(
            in TeleportFrame frame,
            in PhysicsTeleportData data,
            float radius,
            in CollisionWorld collisionWorld,
            out float3 landing)
        {
            var candidateCount = math.clamp(data.MaxCandidates, MinCandidates, MaxCandidates);

            for (var c = 0; c < candidateCount; c++)
            {
                TeleportMath.GenerateCandidate(
                    c, candidateCount,
                    data.AzimuthCenter, data.AzimuthHalfRange,
                    data.ElevationCenter, data.ElevationHalfRange,
                    frame.ReferenceRotation, radius, frame.LandingPosition,
                    out var candidate);

                if (!TeleportMath.CheckClearance(
                        in collisionWorld, candidate, data.ClearanceRadius, data.ObstacleMask, frame.TeleportedEntity))
                    continue;

                if (data.RequireCandidateVisibility &&
                    !TeleportMath.CheckLineOfSight(
                        in collisionWorld, candidate, frame.LandingPosition,
                        data.LineOfSightOffset, data.ObstacleMask, frame.TeleportedEntity, frame.LandingEntity))
                    continue;

                landing = candidate;
                return true;
            }

            landing = float3.zero;
            return false;
        }
    }
}