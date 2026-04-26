using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsTriggerResolution
    {
        public static bool TryResolvePosition(PhysicsTriggerPositionMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactPoint, out float3 position)
        {
            position = mode switch
            {
                PhysicsTriggerPositionMode.MatchCollidedEntity => other.Position,
                PhysicsTriggerPositionMode.MatchContactPoint => contactPoint,
                _ => self.Position
            };
            return true;
        }

        public static bool TryResolveRotation(PhysicsTriggerRotationMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactNormal, out quaternion rotation)
        {
            rotation = mode switch
            {
                PhysicsTriggerRotationMode.MatchSelf => math.quaternion(self.Value),
                PhysicsTriggerRotationMode.MatchCollidedEntity => math.quaternion(other.Value),
                PhysicsTriggerRotationMode.AlignToContactNormal =>
                    quaternion.LookRotationSafe(contactNormal, math.up()),
                PhysicsTriggerRotationMode.Identity => quaternion.identity,
                _ => quaternion.identity
            };
            return true;
        }

        public static bool TryResolveTarget(Target mode, Entity self, Entity other, in Targets targets, in ComponentLookup<TargetsCustom> customLookup, out Entity target)
        {
            target = mode switch
            {
                Target.Self => self,
                Target.Target => other,
                Target.Owner => targets.Owner,
                Target.Source => targets.Source,
                Target.Custom0 => customLookup.TryGetComponent(self, out var tc0) ? tc0.Target0 : Entity.Null,
                Target.Custom1 => customLookup.TryGetComponent(self, out var tc1) ? tc1.Target1 : Entity.Null,
                _ => Entity.Null
            };
            return target != Entity.Null;
        }

        public static bool TryResolveLinkRuntime(Entity target, byte linkId, in ComponentLookup<Parent> parentLookup, in BufferLookup<EntityLinkElement> linkBuffer, out Entity resolvedTarget)
        {
            resolvedTarget = Entity.Null;
            if (linkId == 0) return false;

            var current = target;
            for (var i = 0; i < 32; i++)
            {
                if (linkBuffer.TryGetBuffer(current, out var links))
                    return links.TryGetLink(linkId, out resolvedTarget);

                if (parentLookup.TryGetComponent(current, out var parent))
                    current = parent.Value;
                else
                    break;
            }
            
            return false;
        }

        public static bool TryCalculateTransform(
            PhysicsTriggerPositionMode posMode, float3 resolvedPosOffset,
            PhysicsTriggerRotationMode rotMode, float3 rotOffsetEuler,
            LocalToWorld self, LocalToWorld other, float3 contactPoint, float3 contactNormal,
            out LocalTransform transform)
        {
            TryResolvePosition(posMode, self, other, contactPoint, out var pos);
            TryResolveRotation(rotMode, self, other, contactNormal, out var rot);

            pos += resolvedPosOffset;
            rot = math.mul(rot, quaternion.Euler(rotOffsetEuler));

            transform = LocalTransform.FromPositionRotation(pos, rot);
            return true;
        }
    }
}