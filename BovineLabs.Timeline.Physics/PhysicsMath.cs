using BovineLabs.Core.Extensions;
using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsMath
    {
        public static bool TryCalculatePid(float3 error, PidTuning tuning, PidStateData state, float deltaTime, out float3 output, out PidStateData nextState)
        {
            if (deltaTime <= 0f)
            {
                output = float3.zero;
                nextState = state;
                return false;
            }

            var isInit = state.IsInitialized;
            var prevError = math.select(error, state.PreviousError, isInit);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInit);

            var nextIntegral = integral + (error * deltaTime);
            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            nextIntegral = math.clamp(nextIntegral, -integralMax, integralMax);

            var derivative = (error - prevError) / deltaTime;

            var rawOutput = (tuning.Proportional * error)
                          + (tuning.Integral * nextIntegral)
                          + (tuning.Derivative * derivative);

            var magSq = math.lengthsq(rawOutput);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            output = magSq > maxSq ? math.normalize(rawOutput) * tuning.MaxOutput : rawOutput;

            nextState = new PidStateData
            {
                IntegralAccumulator = nextIntegral,
                PreviousError = error,
                IsInitialized = true
            };

            return true;
        }

        public static bool TryCalculateAngularError(quaternion current, quaternion target, out float3 error)
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

        public static bool TryResolveAngularPidTarget(LocalTransform transform, PhysicsAngularPIDData config, Entity entity, UnsafeComponentLookup<Targets> targetsLookup, ComponentLookup<TargetsCustom> targetsCustoms, UnsafeComponentLookup<LocalTransform> transformLookup, out quaternion targetRotation)
        {
            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity, targetsCustoms);

            if (targetEntity == Entity.Null || !transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                targetTransform = transform;

            targetRotation = config.TargetMode switch
            {
                PidAngularTargetMode.MatchTarget => math.mul(targetTransform.Rotation, config.TargetRotation),
                PidAngularTargetMode.LookAtTarget => ResolveLookAtTarget(transform.Position, targetTransform.Position, transform.Rotation, config.TargetRotation),
                PidAngularTargetMode.World => config.TargetRotation,
                _ => transform.Rotation
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

        private static quaternion ResolveLookAtTarget(float3 selfPos, float3 targetPos, quaternion selfRot, quaternion offsetRot)
        {
            var diff = targetPos - selfPos;
            var dir = math.lengthsq(diff) > 1e-5f ? math.normalize(diff) : math.mul(selfRot, math.forward());
            var baseRot = quaternion.LookRotationSafe(dir, math.up());
            return math.mul(baseRot, offsetRot);
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
}
