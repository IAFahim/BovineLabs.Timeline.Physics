using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    public static class PhysicsTriggerResolution
    {
        public static bool TryResolvePosition(
            PhysicsTriggerPositionMode mode,
            float3 selfPos,
            float3 otherPos,
            float3 contactPoint,
            out float3 position)
        {
            position = mode switch
            {
                PhysicsTriggerPositionMode.MatchSelf => selfPos,
                PhysicsTriggerPositionMode.MatchCollidedEntity => otherPos,
                PhysicsTriggerPositionMode.MatchContactPoint => contactPoint,
                _ => contactPoint
            };

            return true;
        }

        public static bool TryResolvePosition(
            PhysicsTriggerPositionMode mode,
            LocalToWorld self,
            LocalToWorld other,
            float3 contactPoint,
            out float3 position)
        {
            position = mode switch
            {
                PhysicsTriggerPositionMode.MatchSelf => self.Position,
                PhysicsTriggerPositionMode.MatchCollidedEntity => other.Position,
                PhysicsTriggerPositionMode.MatchContactPoint => contactPoint,
                _ => contactPoint
            };

            return true;
        }

        public static bool TryResolveRotation(
            PhysicsTriggerRotationMode mode,
            LocalToWorld self,
            LocalToWorld other,
            float3 contactNormal,
            out quaternion rotation)
        {
            rotation = mode switch
            {
                PhysicsTriggerRotationMode.MatchSelf => new quaternion(math.orthonormalize(new float3x3(self.Value))),
                PhysicsTriggerRotationMode.MatchCollidedEntity => new quaternion(
                    math.orthonormalize(new float3x3(other.Value))),
                PhysicsTriggerRotationMode.AlignToContactNormal =>
                    quaternion.LookRotationSafe(contactNormal, math.up()),
                PhysicsTriggerRotationMode.Identity => quaternion.identity,
                _ => quaternion.identity
            };

            return true;
        }

        public static bool TryResolveTarget(
            Target mode,
            Entity self,
            Entity other,
            in Targets targets,
            out Entity target)
        {
            target = mode switch
            {
                Target.Self => self,
                Target.Target => other,
                Target.Owner => targets.Owner,
                Target.Source => targets.Source,
                Target.Custom => targets.Custom,
                _ => Entity.Null
            };

            return target != Entity.Null;
        }

        public static bool TryResolveLinkedTarget(
            Target targetMode,
            ushort linkKey,
            Entity self,
            Entity other,
            in Targets targets,
            in UnsafeComponentLookup<EntityLinkSource> sources,
            in UnsafeBufferLookup<EntityLinkEntry> links,
            out Entity resolved)
        {
            resolved = Entity.Null;

            if (!TryResolveTarget(targetMode, self, other, targets, out var target)) return false;

            if (linkKey == 0)
            {
                resolved = target;
                return true;
            }

            if (EntityLinkResolver.TryResolve(target, linkKey, sources, links, out var linked))
            {
                resolved = linked;
                return true;
            }

            resolved = target;
            return true;
        }

        public static bool TryCalculateTransform(
            PhysicsTriggerPositionMode positionMode,
            float3 resolvedPositionOffset,
            PhysicsTriggerRotationMode rotationMode,
            float3 rotationOffsetEuler,
            LocalToWorld self,
            LocalToWorld other,
            float3 contactPoint,
            float3 contactNormal,
            out LocalTransform transform)
        {
            TryResolvePosition(positionMode, self, other, contactPoint, out var position);
            TryResolveRotation(rotationMode, self, other, contactNormal, out var rotation);

            position += resolvedPositionOffset;
            rotation = math.mul(rotation, quaternion.Euler(rotationOffsetEuler));

            transform = LocalTransform.FromPositionRotation(position, rotation);
            return true;
        }
    }
}