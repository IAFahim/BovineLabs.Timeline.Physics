using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsMath
    {
        public static bool TryResolveLinearPidTarget(LocalTransform transform, PhysicsLinearPIDData config, Entity entity, UnsafeComponentLookup<Targets> targetsLookup, ComponentLookup<TargetsCustom> targetsCustoms, UnsafeComponentLookup<LocalTransform> transformLookup, out float3 targetPosition)
        {
            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity, targetsCustoms);

            if (targetEntity == Entity.Null || !transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                targetTransform = transform;

            targetPosition = config.TargetMode switch
            {
                PidLinearTargetMode.TargetLocal => targetTransform.Position + math.rotate(targetTransform.Rotation, config.TargetOffset),
                PidLinearTargetMode.LineOfSight => ResolveLineOfSight(transform.Position, targetTransform.Position, transform.Rotation, config.TargetOffset),
                PidLinearTargetMode.World => targetTransform.Position + config.TargetOffset,
                _ => transform.Position
            };

            return true;
        }

        private static float3 ResolveLineOfSight(float3 selfPos, float3 targetPos, quaternion selfRot, float3 offset)
        {
            var diff = targetPos - selfPos;
            var dir = math.lengthsq(diff) > 1e-5f ? math.normalize(diff) : math.mul(selfRot, math.forward());
            var rot = quaternion.LookRotationSafe(dir, math.up());
            return targetPos + math.rotate(rot, offset);
        }

        public static bool TryCalculateLinearPidForce(float3 currentPosition, float3 targetPosition, PhysicsLinearPIDData config, PhysicsLinearPIDState state, float deltaTime, out float3 force, out float3 nextIntegral, out float3 nextPrevError)
        {
            if (deltaTime <= 0f)
            {
                force = float3.zero;
                nextIntegral = state.IntegralAccumulator;
                nextPrevError = state.PreviousError;
                return false;
            }

            var error = targetPosition - currentPosition;
            var isInitialized = state.IsInitialized;

            var prevError = math.select(error, state.PreviousError, isInitialized);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInitialized);

            nextIntegral = integral + (error * deltaTime);
            var derivative = (error - prevError) / deltaTime;

            var rawForce = (config.Proportional * error)
                         + (config.Integral * nextIntegral)
                         + (config.Derivative * derivative);

            var forceMagSq = math.lengthsq(rawForce);
            var maxForceSq = config.MaxForce * config.MaxForce;

            force = forceMagSq > maxForceSq ? math.normalize(rawForce) * config.MaxForce : rawForce;
            nextPrevError = error;
            return true;
        }

        public static bool TryResolveAngularPidTarget(LocalTransform transform, PhysicsAngularPIDData config, Entity entity, UnsafeComponentLookup<Targets> targetsLookup, ComponentLookup<TargetsCustom> targetsCustoms, UnsafeComponentLookup<LocalTransform> transformLookup, out quaternion targetRotation)
        {
            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity, targetsCustoms);

            if (targetEntity == Entity.Null || !transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                targetTransform = transform;

            var offsetRot = quaternion.Euler(config.TargetRotationEuler);

            targetRotation = config.TargetMode switch
            {
                PidAngularTargetMode.MatchTarget => math.mul(targetTransform.Rotation, offsetRot),
                PidAngularTargetMode.LookAtTarget => ResolveLookAtTarget(transform.Position, targetTransform.Position, transform.Rotation, offsetRot),
                PidAngularTargetMode.World => offsetRot,
                _ => transform.Rotation
            };

            return true;
        }

        private static quaternion ResolveLookAtTarget(float3 selfPos, float3 targetPos, quaternion selfRot, quaternion offsetRot)
        {
            var diff = targetPos - selfPos;
            var dir = math.lengthsq(diff) > 1e-5f ? math.normalize(diff) : math.mul(selfRot, math.forward());
            var baseRot = quaternion.LookRotationSafe(dir, math.up());
            return math.mul(baseRot, offsetRot);
        }

        public static bool TryCalculateAngularPidTorque(quaternion currentRot, quaternion targetRot, PhysicsAngularPIDData config, PhysicsAngularPIDState state, float deltaTime, out float3 torque, out float3 nextIntegral, out float3 nextPrevError)
        {
            if (deltaTime <= 0f || !TryCalculateAngularError(currentRot, targetRot, out var error))
            {
                torque = float3.zero;
                nextIntegral = state.IntegralAccumulator;
                nextPrevError = state.PreviousError;
                return false;
            }

            var isInitialized = state.IsInitialized;
            var prevError = math.select(error, state.PreviousError, isInitialized);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInitialized);

            nextIntegral = integral + (error * deltaTime);
            var derivative = (error - prevError) / deltaTime;

            var rawTorque = (config.Proportional * error)
                          + (config.Integral * nextIntegral)
                          + (config.Derivative * derivative);

            var torqueMagSq = math.lengthsq(rawTorque);
            var maxTorqueSq = config.MaxTorque * config.MaxTorque;

            torque = torqueMagSq > maxTorqueSq ? math.normalize(rawTorque) * config.MaxTorque : rawTorque;
            nextPrevError = error;
            return true;
        }

        private static bool TryCalculateAngularError(quaternion current, quaternion target, out float3 error)
        {
            var delta = math.mul(target, math.inverse(current));
            var q = delta.value;
            if (q.w < 0f) q = -q;
            
            var dot = math.lengthsq(q.xyz);
            if (dot < 1e-6f)
            {
                error = float3.zero;
                return true;
            }
            
            var angle = 2.0f * math.acos(math.clamp(q.w, -1f, 1f));
            error = (q.xyz / math.sqrt(dot)) * angle;
            return true;
        }

        public static bool TryApplyLinearForce(PhysicsVelocity velocityIn, PhysicsMass mass, float3 force, float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            var invMass = mass.InverseMass > 0f ? mass.InverseMass : 1f;
            velocityOut.Linear += force * invMass * deltaTime;
            return true;
        }

        public static bool TryApplyAngularTorque(PhysicsVelocity velocityIn, PhysicsMass mass, LocalTransform transform, float3 torque, float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            var invInertia = math.any(mass.InverseInertia > 0f) ? mass.InverseInertia : new float3(1f);
            var localTorque = math.rotate(math.inverse(transform.Rotation), torque);
            var localAngularVelocityChange = localTorque * invInertia * deltaTime;
            velocityOut.Angular += math.rotate(transform.Rotation, localAngularVelocityChange);
            return true;
        }
    }

    public static class TriggerResolution
    {
        private static bool TryResolvePosition(InstantiatePositionMode mode, LocalToWorld self, LocalToWorld other, float3 contactPoint, bool hasContact, out float3 position)
        {
            position = mode switch
            {
                InstantiatePositionMode.MatchCollidedEntity => other.Position,
                InstantiatePositionMode.MatchContactPoint => hasContact ? contactPoint : (self.Position + other.Position) * 0.5f,
                _ => self.Position
            };
            return true;
        }

        private static bool TryResolveRotation(InstantiateRotationMode mode, LocalToWorld self, LocalToWorld other, float3 contactNormal, bool hasContact, out quaternion rotation)
        {
            rotation = mode switch
            {
                InstantiateRotationMode.MatchSelf => math.quaternion(self.Value),
                InstantiateRotationMode.MatchCollidedEntity => math.quaternion(other.Value),
                InstantiateRotationMode.AlignToContactNormal => hasContact
                    ? quaternion.LookRotationSafe(contactNormal, math.up())
                    : quaternion.LookRotationSafe(math.normalize(self.Position - other.Position), math.up()),
                _ => quaternion.identity
            };
            return true;
        }

        public static bool TryResolveLocalTransform(PhysicsTriggerInstantiateData cfg, LocalToWorld self, LocalToWorld other, float3 contactPoint, float3 contactNormal, bool hasContact, out LocalTransform transform)
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

        public static bool TryResolveParent(InstantiateParentMode mode, Entity self, Entity other, UnsafeComponentLookup<Targets> targetsLookup, out Entity parent)
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

        public static bool TryResolveRelativeTransform(LocalTransform worldTransform, Entity parent, UnsafeComponentLookup<LocalToWorld> ltwLookup, out LocalTransform localTransform)
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

        public static bool TryResolveTargets(Entity self, Entity other, UnsafeComponentLookup<Targets> targetsLookup, out Targets targets)
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