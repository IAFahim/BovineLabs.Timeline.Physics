using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsMath
    {
        public static bool TryResolveSpaceVector(
            Target space,
            float3 vector,
            Entity entity,
            in ComponentLookup<Targets> targetsLookup,
            in ComponentLookup<TargetsCustom> customLookup,
            in UnsafeComponentLookup<LocalTransform> transformLookup,
            out float3 resolvedVector)
        {
            if (space == Target.None)
            {
                resolvedVector = vector;
                return true;
            }

            var targetEntity = entity;
            if (space != Target.Self && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(space, entity, customLookup);

            if (targetEntity != Entity.Null && transformLookup.TryGetComponent(targetEntity, out var lt))
            {
                resolvedVector = math.rotate(lt.Rotation, vector);
                return true;
            }

            resolvedVector = vector;
            return true;
        }

        public static bool TryComputeExponentialDecay(in PhysicsVelocity velocityIn, in PhysicsDragData drag,
            float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            if (deltaTime <= 0f) return true;

            velocityOut.Linear *= math.exp(-drag.Linear * deltaTime);
            velocityOut.Angular *= math.exp(-drag.Angular * deltaTime);
            return true;
        }

        public static bool TryDrawLinearPidPrediction(ref Drawer drawer, float3 startPos, float3 targetPos,
            PidTuning tuning, float time)
        {
            var pos = startPos;
            var vel = float3.zero;
            var integral = float3.zero;
            var prevError = targetPos - startPos;

            const float dt = 0.02f;
            var steps = (int)(time / dt);
            var lastPos = startPos;

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            for (var i = 0; i < steps; i++)
            {
                var error = targetPos - pos;
                integral += error * dt;
                integral = math.clamp(integral, -integralMax, integralMax);

                var derivative = (error - prevError) / dt;
                var rawForce = tuning.Proportional * error + tuning.Integral * integral +
                               tuning.Derivative * derivative;

                var forceMagSq = math.lengthsq(rawForce);
                var force = forceMagSq > maxSq ? math.normalize(rawForce) * tuning.MaxOutput : rawForce;

                vel += force * dt;
                pos += vel * dt;
                prevError = error;

                drawer.Line(lastPos, pos, new Color(0f, 1f, 0f, 0.3f));
                lastPos = pos;

                if (math.lengthsq(error) < 1e-4f && forceMagSq < 1e-4f && math.lengthsq(vel) < 1e-4f)
                {
                    pos = targetPos;
                    break;
                }
            }

            drawer.Cuboid(pos, quaternion.identity, new float3(0.5f), Color.green);
            drawer.Text32(pos + new float3(0, 0.5f, 0), "Predicted", Color.green, 10f);
            return true;
        }

        public static bool TryDrawAngularPidPrediction(ref Drawer drawer, float3 drawPos, quaternion startRot,
            quaternion targetRot, PidTuning tuning, float time)
        {
            var rot = startRot;
            var vel = float3.zero;
            var integral = float3.zero;

            if (!TryComputeAngularError(rot, targetRot, out var prevError)) return false;

            const float dt = 0.02f;
            var steps = (int)(time / dt);

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            for (var i = 0; i < steps; i++)
            {
                if (!TryComputeAngularError(rot, targetRot, out var error)) continue;

                integral += error * dt;
                integral = math.clamp(integral, -integralMax, integralMax);

                var derivative = (error - prevError) / dt;
                var rawTorque = tuning.Proportional * error + tuning.Integral * integral +
                                tuning.Derivative * derivative;

                var torqueMagSq = math.lengthsq(rawTorque);
                var torque = torqueMagSq > maxSq ? math.normalize(rawTorque) * tuning.MaxOutput : rawTorque;

                vel += torque * dt;

                var localDelta = vel * dt;
                var mag = math.length(localDelta);
                if (mag > 1e-6f)
                {
                    var dq = quaternion.AxisAngle(localDelta / mag, mag);
                    rot = math.normalize(math.mul(rot, dq));
                }

                prevError = error;

                if (i % 10 == 0)
                    drawer.Arrow(drawPos, math.mul(rot, math.forward()) * 0.5f, new Color(0f, 1f, 0f, 0.1f));

                if (math.lengthsq(error) < 1e-4f && torqueMagSq < 1e-4f && math.lengthsq(vel) < 1e-4f)
                {
                    rot = targetRot;
                    break;
                }
            }

            drawer.Arrow(drawPos, math.mul(rot, math.forward()), Color.green);
            drawer.Arrow(drawPos, math.mul(rot, math.up()), new Color(0f, 0.5f, 0f));
            drawer.Text32(drawPos + new float3(0, -0.5f, 0), "Predicted", Color.green, 10f);
            return true;
        }

        public static bool TryComputePidForce(float3 error, PidTuning tuning, PidStateData state, float deltaTime,
            out float3 output, out PidStateData nextState)
        {
            if (deltaTime <= 0f)
            {
                output = float3.zero;
                nextState = state;
                return true;
            }

            var isInit = state.IsInitialized;
            var prevError = math.select(error, state.PreviousError, isInit);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInit);

            var nextIntegral = integral + error * deltaTime;
            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            nextIntegral = math.clamp(nextIntegral, -integralMax, integralMax);

            var derivative = (error - prevError) / deltaTime;

            var rawOutput = tuning.Proportional * error
                            + tuning.Integral * nextIntegral
                            + tuning.Derivative * derivative;

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

        public static bool TryComputeAngularError(quaternion current, quaternion target, out float3 error)
        {
            var delta = math.mul(target, math.conjugate(current));
            var q = delta.value;
            var qPositive = math.select(q, -q, q.w < 0f);

            var dot = math.lengthsq(qPositive.xyz);
            if (dot < 1e-6f)
            {
                error = float3.zero;
                return true;
            }

            var angle = 2.0f * math.acos(math.clamp(qPositive.w, -1f, 1f));
            error = qPositive.xyz / math.sqrt(dot) * angle;
            return true;
        }

        public static bool TryResolveLinearPidTarget(
            in LocalTransform transform,
            in PhysicsLinearPIDData config,
            Entity entity,
            in ComponentLookup<Targets> targetsLookup,
            in ComponentLookup<TargetsCustom> targetsCustoms,
            in UnsafeComponentLookup<LocalTransform> transformLookup,
            out float3 targetPosition)
        {
            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity, targetsCustoms);

            if (targetEntity == Entity.Null || !transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                targetTransform = transform;

            targetPosition = config.TargetMode switch
            {
                PidLinearTargetMode.TargetLocal or
                PidLinearTargetMode.InitialLocal => targetTransform.Position +
                                                    math.rotate(targetTransform.Rotation, config.TargetOffset),
                PidLinearTargetMode.LineOfSight => ResolveLineOfSight(transform.Position, targetTransform.Position,
                    transform.Rotation, config.TargetOffset),
                PidLinearTargetMode.World => config.TargetOffset,
                _ => transform.Position
            };

            return true;
        }

        public static bool TryResolveAngularPidTarget(
            in LocalTransform transform,
            in PhysicsAngularPIDData config,
            Entity entity,
            in ComponentLookup<Targets> targetsLookup,
            in ComponentLookup<TargetsCustom> targetsCustoms,
            in UnsafeComponentLookup<LocalTransform> transformLookup,
            out quaternion targetRotation)
        {
            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity, targetsCustoms);

            if (targetEntity == Entity.Null || !transformLookup.TryGetComponent(targetEntity, out var targetTransform))
                targetTransform = transform;

            targetRotation = config.TargetMode switch
            {
                PidAngularTargetMode.MatchTarget => math.mul(targetTransform.Rotation, config.TargetRotation),
                PidAngularTargetMode.LookAtTarget => ResolveLookAtTarget(transform.Position, targetTransform.Position,
                    transform.Rotation, config.TargetRotation),
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

        private static quaternion ResolveLookAtTarget(float3 selfPos, float3 targetPos, quaternion selfRot,
            quaternion offsetRot)
        {
            var diff = targetPos - selfPos;
            var dir = math.lengthsq(diff) > 1e-5f ? math.normalize(diff) : math.mul(selfRot, math.forward());
            var baseRot = quaternion.LookRotationSafe(dir, math.up());
            return math.mul(baseRot, offsetRot);
        }

        public static bool TryApplyLinearForce(in PhysicsVelocity velocityIn, in PhysicsMass mass, float3 force,
            float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            var invMass = mass.InverseMass > 0f ? mass.InverseMass : 1f;
            velocityOut.Linear += force * invMass * deltaTime;
            return true;
        }

        public static bool TryApplyAngularTorque(in PhysicsVelocity velocityIn, in PhysicsMass mass,
            in LocalTransform transform, float3 torque, float deltaTime, out PhysicsVelocity velocityOut)
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