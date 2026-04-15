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
        public static bool TryResolveLinearPidTarget(LocalTransform transform, PhysicsLinearPIDData config, Entity entity, UnsafeComponentLookup<Targets> targetsLookup, UnsafeComponentLookup<LocalTransform> transformLookup, out float3 targetPosition)
        {
            var selfGoalPos = transform.Position + math.rotate(transform.Rotation, config.LocalTargetOffset);

            if (config.ChaseTargetBlend > 0.001f && targetsLookup.TryGetComponent(entity, out var targets) && transformLookup.TryGetComponent(targets.Target, out var enemyTransform))
            {
                var enemyGoalPos = enemyTransform.Position + math.rotate(enemyTransform.Rotation, config.LocalTargetOffset);
                targetPosition = math.lerp(selfGoalPos, enemyGoalPos, config.ChaseTargetBlend);
                return true;
            }

            targetPosition = selfGoalPos;
            return true;
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

        public static bool TryResolveAngularPidTarget(LocalTransform transform, PhysicsAngularPIDData config, Entity entity, UnsafeComponentLookup<Targets> targetsLookup, UnsafeComponentLookup<LocalTransform> transformLookup, out quaternion targetRotation)
        {
            var selfGoalRot = math.mul(transform.Rotation, quaternion.Euler(config.LocalTargetRotationEuler));

            if (config.ChaseTargetBlend > 0.001f && targetsLookup.TryGetComponent(entity, out var targets) && transformLookup.TryGetComponent(targets.Target, out var enemyTransform))
            {
                var enemyGoalRot = math.mul(enemyTransform.Rotation, quaternion.Euler(config.LocalTargetRotationEuler));
                targetRotation = math.slerp(selfGoalRot, enemyGoalRot, config.ChaseTargetBlend);
                return true;
            }

            targetRotation = selfGoalRot;
            return true;
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
        private static void TryResolvePosition(InstantiatePositionMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactPoint, bool hasContact, out float3 position)
        {
            position = mode switch
            {
                InstantiatePositionMode.MatchCollidedEntity => other.Position,
                InstantiatePositionMode.MatchContactPoint => hasContact ? contactPoint : (self.Position + other.Position) * 0.5f,
                _ => self.Position
            };
        }

        private static void TryResolveRotation(InstantiateRotationMode mode, LocalToWorld self, LocalToWorld other,
            float3 contactNormal, bool hasContact, out quaternion rotation)
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
        }
    }
}