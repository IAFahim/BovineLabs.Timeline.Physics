using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsMath
    {
        public static bool TryCalculatePIDForce(in float3 currentPosition, in float3 targetPosition, in PhysicsPIDData config, in PhysicsPIDState state, in float deltaTime, out float3 force, out PhysicsPIDState nextState)
        {
            if (deltaTime <= 0f)
            {
                force = float3.zero;
                nextState = state;
                return false;
            }

            var error = targetPosition - currentPosition;
            var isInitialized = state.IsInitialized;

            var prevError = math.select(error, state.PreviousError, isInitialized);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInitialized);

            var nextIntegral = integral + (error * deltaTime);
            var derivative = (error - prevError) / deltaTime;

            var rawForce = (config.Proportional * error)
                         + (config.Integral * nextIntegral)
                         + (config.Derivative * derivative);

            var forceMagSq = math.lengthsq(rawForce);
            var maxForceSq = config.MaxForce * config.MaxForce;

            force = forceMagSq > maxForceSq
                ? math.normalize(rawForce) * config.MaxForce
                : rawForce;

            nextState = new PhysicsPIDState
            {
                IntegralAccumulator = nextIntegral,
                PreviousError = error,
                IsInitialized = true
            };

            return true;
        }

        public static bool TryResolvePIDTarget(in LocalTransform transform, in PhysicsPIDData config, in Entity entity, in UnsafeComponentLookup<Targets> targetsLookup, in UnsafeComponentLookup<LocalTransform> transformLookup, out float3 targetPosition)
        {
            var selfGoal = transform.Position + math.rotate(transform.Rotation, config.LocalTargetOffset);

            if (config.ChaseTargetBlend > 0.001f && targetsLookup.TryGetComponent(entity, out var targets) && transformLookup.TryGetComponent(targets.Target, out var enemyTransform))
            {
                var enemyGoal = enemyTransform.Position + math.rotate(enemyTransform.Rotation, config.LocalTargetOffset);
                targetPosition = math.lerp(selfGoal, enemyGoal, config.ChaseTargetBlend);
                return true;
            }

            targetPosition = selfGoal;
            return true;
        }
    }

    public static class TriggerResolution
    {
        public static bool TryResolvePosition(in InstantiatePositionMode mode, in LocalToWorld self, in LocalToWorld other, in float3 contactPoint, in bool hasContact, out float3 position)
        {
            position = mode switch
            {
                InstantiatePositionMode.MatchSelf => self.Position,
                InstantiatePositionMode.MatchCollidedEntity => other.Position,
                InstantiatePositionMode.MatchContactPoint => hasContact ? contactPoint : (self.Position + other.Position) * 0.5f,
                _ => self.Position
            };
            return true;
        }

        public static bool TryResolveRotation(in InstantiateRotationMode mode, in LocalToWorld self, in LocalToWorld other, in float3 contactNormal, in bool hasContact, out quaternion rotation)
        {
            var diff = math.normalize(self.Position - other.Position);
            var fallbackNormal = math.lengthsq(diff) > 0 ? diff : math.forward();

            rotation = mode switch
            {
                InstantiateRotationMode.MatchSelf => math.quaternion(self.Value),
                InstantiateRotationMode.MatchCollidedEntity => math.quaternion(other.Value),
                InstantiateRotationMode.AlignToContactNormal => hasContact
                    ? quaternion.LookRotationSafe(contactNormal, math.up())
                    : quaternion.LookRotationSafe(fallbackNormal, math.up()),
                InstantiateRotationMode.Identity => quaternion.identity,
                _ => quaternion.identity
            };
            return true;
        }

        public static bool TryResolveLocalTransform(in PhysicsTriggerInstantiateData cfg, in LocalToWorld self, in LocalToWorld other, in float3 contactPoint, in float3 contactNormal, in bool hasContact, out LocalTransform transform)
        {
            TryResolvePosition(cfg.PositionMode, self, other, contactPoint, hasContact, out var position);
            TryResolveRotation(cfg.RotationMode, self, other, contactNormal, hasContact, out var rotation);

            var positionOffset = cfg.IsPositionOffsetLocal
                ? math.rotate(rotation, cfg.PositionOffset)
                : cfg.PositionOffset;

            var finalPosition = position + positionOffset;
            var finalRotation = math.mul(rotation, quaternion.Euler(cfg.RotationOffsetEuler));

            transform = LocalTransform.FromPositionRotation(finalPosition, finalRotation);
            return true;
        }

        public static bool TryResolveParent(in InstantiateParentMode mode, in Entity self, in Entity other, in UnsafeComponentLookup<Targets> targetsLookup, out Entity parent)
        {
            parent = mode switch
            {
                InstantiateParentMode.ParentToCollidedEntity => other,
                _ => Entity.Null
            };

            if (mode >= InstantiateParentMode.ParentToReactionOwner && targetsLookup.TryGetComponent(self, out var targets))
            {
                parent = mode switch
                {
                    InstantiateParentMode.ParentToReactionOwner => targets.Owner,
                    InstantiateParentMode.ParentToReactionSource => targets.Source,
                    InstantiateParentMode.ParentToReactionTarget => targets.Target,
                    _ => parent
                };
            }

            return parent != Entity.Null;
        }

        public static bool TryResolveRelativeTransform(in LocalTransform worldTransform, in Entity parent, in UnsafeComponentLookup<LocalToWorld> ltwLookup, out LocalTransform localTransform)
        {
            if (ltwLookup.TryGetComponent(parent, out var parentLtw))
            {
                var worldMatrix = float4x4.TRS(worldTransform.Position, worldTransform.Rotation, worldTransform.Scale);
                var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
                localMatrix.ExtractLocalTransform(out localTransform);
                return true;
            }

            localTransform = default;
            return false;
        }

        public static bool TryResolveTargets(in Entity self, in Entity other, in UnsafeComponentLookup<Targets> targetsLookup, out Targets targets)
        {
            if (targetsLookup.TryGetComponent(self, out var selfTargets) && targetsLookup.TryGetComponent(other, out var otherTargets))
            {
                targets = new Targets
                {
                    Owner = selfTargets.Owner,
                    Source = selfTargets.Source,
                    Target = otherTargets.Source
                };
                return true;
            }
            targets = default;
            return false;
        }
    }
}