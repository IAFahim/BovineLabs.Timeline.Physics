using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsTriggerResolution
    {
        public static float3 ResolvePosition(PhysicsTriggerPositionMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactPoint)
        {
            return mode switch
            {
                PhysicsTriggerPositionMode.MatchCollidedEntity => other.Position,
                PhysicsTriggerPositionMode.MatchContactPoint => contactPoint,
                _ => self.Position
            };
        }

        public static quaternion ResolveRotation(PhysicsTriggerRotationMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactNormal)
        {
            return mode switch
            {
                PhysicsTriggerRotationMode.MatchSelf => math.quaternion(self.Value),
                PhysicsTriggerRotationMode.MatchCollidedEntity => math.quaternion(other.Value),
                PhysicsTriggerRotationMode.AlignToContactNormal =>
                    quaternion.LookRotationSafe(contactNormal, math.up()),
                PhysicsTriggerRotationMode.Identity => quaternion.identity,
                _ => quaternion.identity
            };
        }

        public static Entity ResolveTarget(PhysicsTriggerTargetMode mode, Entity self, Entity other, in Targets targets)
        {
            return mode switch
            {
                PhysicsTriggerTargetMode.Self => self,
                PhysicsTriggerTargetMode.CollidedEntity => other,
                PhysicsTriggerTargetMode.ReactionOwner => targets.Owner,
                PhysicsTriggerTargetMode.ReactionSource => targets.Source,
                PhysicsTriggerTargetMode.ReactionTarget => targets.Target,
                _ => Entity.Null
            };
        }

        public static LocalTransform CalculateTransform(
            PhysicsTriggerPositionMode posMode, float3 posOffset, bool isPosOffsetLocal,
            PhysicsTriggerRotationMode rotMode, float3 rotOffsetEuler,
            LocalToWorld self, LocalToWorld other, float3 contactPoint, float3 contactNormal)
        {
            var pos = ResolvePosition(posMode, self, other, contactPoint);
            var rot = ResolveRotation(rotMode, self, other, contactNormal);

            pos += isPosOffsetLocal ? math.rotate(rot, posOffset) : posOffset;
            rot = math.mul(rot, quaternion.Euler(rotOffsetEuler));

            return LocalTransform.FromPositionRotation(pos, rot);
        }
    }
}