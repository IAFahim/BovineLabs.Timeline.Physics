using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsTriggerResolution
    {
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
                PhysicsTriggerRotationMode.MatchSelf => new quaternion(self.Value),
                PhysicsTriggerRotationMode.MatchCollidedEntity => new quaternion(other.Value),
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
            in ComponentLookup<TargetsCustom> customLookup,
            out Entity target)
        {
            target = mode switch
            {
                Target.Self => self,
                Target.Target => other,
                Target.Owner => targets.Owner,
                Target.Source => targets.Source,
                Target.Custom0 => customLookup.TryGetComponent(self, out var custom) ? custom.Target0 : Entity.Null,
                Target.Custom1 => customLookup.TryGetComponent(self, out var custom) ? custom.Target1 : Entity.Null,
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
            in ComponentLookup<TargetsCustom> customLookup,
            in ComponentLookup<EntityLinkSource> sources,
            in BufferLookup<EntityLinkEntry> links,
            out Entity resolved)
        {
            resolved = Entity.Null;

            if (!TryResolveTarget(targetMode, self, other, targets, customLookup, out var target)) return false;

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