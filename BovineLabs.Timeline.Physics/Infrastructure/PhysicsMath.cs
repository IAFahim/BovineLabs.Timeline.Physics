using BovineLabs.Core.Iterators;
using BovineLabs.Quill;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    public static class PhysicsMath
    {
        public static RicochetStepResult StepRicochet(
            float3 currentPos,
            float3 currentDir,
            float remainingDistance,
            uint ricochetMask,
            uint terminalHitMask,
            float minGrazingAngle,
            in CollisionWorld collisionWorld,
            in UnsafeComponentLookup<PhysicsCollider> colliderLookup)
        {
            var result = new RicochetStepResult();

            var rayInput = new RaycastInput
            {
                Start = currentPos,
                End = currentPos + currentDir * remainingDistance,
                Filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = ricochetMask | terminalHitMask,
                    GroupIndex = 0
                }
            };

            if (!collisionWorld.CastRay(rayInput, out var hit))
                return result;

            result.HitFound = true;
            result.DistanceTraveled = math.distance(currentPos, hit.Position);
            result.HitPosition = hit.Position;
            result.SurfaceNormal = hit.SurfaceNormal;
            result.HitEntity = hit.Entity;

            var surfaceBelongsTo = 0u;
            if (colliderLookup.HasComponent(hit.Entity))
            {
                var col = colliderLookup[hit.Entity];
                if (col.IsValid) surfaceBelongsTo = col.Value.Value.GetCollisionFilter().BelongsTo;
            }

            var angleFromNormal = math.acos(math.abs(math.dot(currentDir, hit.SurfaceNormal)));
            var grazingAngle = math.PI / 2f - angleFromNormal;

            if (grazingAngle >= minGrazingAngle || (surfaceBelongsTo & terminalHitMask) != 0)
                result.IsTerminal = true;
            else if ((surfaceBelongsTo & ricochetMask) != 0) result.IsRicochet = true;

            return result;
        }

        public static float3 ResolvePosition(
            Entity target,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup)
        {
            if (target == Entity.Null)
                return float3.zero;

            if (localTransformLookup.TryGetComponent(target, out var lt) && !parentLookup.HasComponent(target))
                return lt.Position;

            if (localToWorldLookup.TryGetComponent(target, out var ltw)) return ltw.Position;

            return float3.zero;
        }


        public static quaternion ResolveRotation(
            Entity target,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup)
        {
            if (target == Entity.Null)
                return quaternion.identity;

            if (localTransformLookup.TryGetComponent(target, out var lt) && !parentLookup.HasComponent(target))
                return lt.Rotation;

            if (localToWorldLookup.TryGetComponent(target, out var ltw))
                return new quaternion(math.orthonormalize(new float3x3(ltw.Value)));

            return quaternion.identity;
        }

        public static void ResolveTransform(
            Entity target,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup,
            out float3 position,
            out quaternion rotation)
        {
            if (target == Entity.Null)
            {
                position = float3.zero;
                rotation = quaternion.identity;
                return;
            }

            if (localTransformLookup.TryGetComponent(target, out var lt) && !parentLookup.HasComponent(target))
            {
                position = lt.Position;
                rotation = lt.Rotation;
                return;
            }

            if (localToWorldLookup.TryGetComponent(target, out var ltw))
            {
                position = ltw.Position;
                rotation = new quaternion(math.orthonormalize(new float3x3(ltw.Value)));
                return;
            }

            position = float3.zero;
            rotation = quaternion.identity;
        }

        public static void ResolveSpaceVector(
            Target space,
            float3 vector,
            Entity entity,
            in UnsafeComponentLookup<Targets> targetsLookup,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup,
            out float3 resolvedVector)
        {
            if (space == Target.None)
            {
                resolvedVector = vector;
                return;
            }

            var targetEntity = entity;
            if (space != Target.Self && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(space, entity);

            var rotation = ResolveRotation(targetEntity, in localTransformLookup, in localToWorldLookup,
                in parentLookup);
            if (math.lengthsq(rotation.value.xyz) > 1e-6f || math.abs(rotation.value.w - 1f) > 1e-6f)
            {
                resolvedVector = math.rotate(rotation, vector);
                return;
            }

            resolvedVector = vector;
        }

        public static void ComputeExponentialDecay(in PhysicsVelocity velocityIn, in PhysicsDragData drag,
            float deltaTime, float multiplier, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            if (deltaTime <= 0f) return;

            velocityOut.Linear *= math.exp(-drag.Linear * multiplier * deltaTime);
            velocityOut.Angular *= math.exp(-drag.Angular * multiplier * deltaTime);
        }

        public static void ComputePidForce(float3 error, PidTuning tuning, PidStateData state, float deltaTime,
            out float3 output, out PidStateData nextState)
        {
            if (deltaTime <= 0f)
            {
                output = float3.zero;
                nextState = state;
                return;
            }

            var isInit = state.IsInitialized;
            var prevError = math.select(error, state.PreviousError, isInit);
            var integral = math.select(float3.zero, state.IntegralAccumulator, isInit);

            var nextIntegral = integral + error * deltaTime;

            if (math.all(tuning.Integral <= 0f))
            {
                nextIntegral = float3.zero;
            }
            else
            {
                var safeIntegral = math.max(tuning.Integral, new float3(0.001f));
                var integralMax = tuning.MaxOutput / safeIntegral;
                nextIntegral = math.clamp(nextIntegral, -integralMax, integralMax);
            }

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
                CapturedTargetPosition = state.CapturedTargetPosition,
                IsInitialized = true
            };
        }

        public static void ComputeAngularError(quaternion current, quaternion target, out float3 error)
        {
            var delta = math.mul(target, math.conjugate(current));
            var q = delta.value;
            var qPositive = math.select(q, -q, q.w < 0f);

            var dot = math.lengthsq(qPositive.xyz);
            if (dot < 1e-6f)
            {
                error = float3.zero;
                return;
            }

            var angle = 2.0f * math.acos(math.clamp(qPositive.w, -1f, 1f));
            error = qPositive.xyz / math.sqrt(dot) * angle;
        }

        public static void ResolveLinearPidTarget(
            in PhysicsLinearPIDData config,
            Entity entity,
            in UnsafeComponentLookup<Targets> targetsLookup,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup,
            out float3 targetPosition)
        {
            ResolveTransform(entity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var selfPos, out var selfRot);

            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity);

            ResolveTransform(targetEntity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var targetPos, out var targetRot);

            if (targetEntity == Entity.Null || math.lengthsq(targetPos) < 1e-6f)
            {
                targetPos = selfPos;
                targetRot = selfRot;
            }

            targetPosition = config.TargetMode switch
            {
                PidLinearTargetMode.TargetLocal or
                    PidLinearTargetMode.InitialLocal =>
                    targetPos + math.rotate(targetRot, config.TargetOffset),

                PidLinearTargetMode.LineOfSight =>
                    ResolveLineOfSight(selfPos, targetPos, selfRot, config.TargetOffset),

                PidLinearTargetMode.World => config.TargetOffset,

                PidLinearTargetMode.FleeFromTarget =>
                    selfPos + (selfPos - targetPos),

                _ => selfPos
            };
        }

        public static void ResolveAngularPidTarget(
            in PhysicsAngularPIDData config,
            Entity entity,
            in UnsafeComponentLookup<Targets> targetsLookup,
            in ComponentLookup<LocalTransform> localTransformLookup,
            in UnsafeComponentLookup<LocalToWorld> localToWorldLookup,
            in ComponentLookup<Parent> parentLookup,
            out quaternion targetRotation)
        {
            ResolveTransform(entity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var selfPos, out var selfRot);

            var targetEntity = Entity.Null;
            if (config.TrackingTarget != Target.None && targetsLookup.TryGetComponent(entity, out var targets))
                targetEntity = targets.Get(config.TrackingTarget, entity);

            ResolveTransform(targetEntity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var targetPos, out var targetRot);

            if (targetEntity == Entity.Null || math.lengthsq(targetPos) < 1e-6f)
            {
                targetPos = selfPos;
                targetRot = selfRot;
            }

            targetRotation = config.TargetMode switch
            {
                PidAngularTargetMode.MatchTarget =>
                    math.mul(targetRot, config.TargetRotation),

                PidAngularTargetMode.LookAtTarget =>
                    ResolveLookAtTarget(selfPos, targetPos, selfRot, config.TargetRotation),

                PidAngularTargetMode.World => config.TargetRotation,

                PidAngularTargetMode.FleeFromTarget =>
                    ResolveLookAtTarget(selfPos, selfPos + (selfPos - targetPos), selfRot, config.TargetRotation),

                PidAngularTargetMode.MatchTargetOpposite =>
                    math.mul(math.mul(targetRot, quaternion.AxisAngle(math.up(), math.PI)), config.TargetRotation),

                _ => selfRot
            };
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

        public static void ApplyLinearForce(in PhysicsVelocity velocityIn, in PhysicsMass mass, float3 force,
            float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            var invMass = mass.InverseMass > 0f ? mass.InverseMass : 1f;
            velocityOut.Linear += force * invMass * deltaTime;
        }

        public static void ApplyAngularTorque(in PhysicsVelocity velocityIn, in PhysicsMass mass,
            in LocalToWorld transform, float3 torque, float deltaTime, out PhysicsVelocity velocityOut)
        {
            velocityOut = velocityIn;
            var invInertia = math.any(mass.InverseInertia > 0f) ? mass.InverseInertia : new float3(1f);
            var rotation = new quaternion(math.orthonormalize(new float3x3(transform.Value)));
            var localTorque = math.rotate(math.inverse(rotation), torque);
            var localAngularVelocityChange = localTorque * invInertia * deltaTime;
            velocityOut.Angular += math.rotate(rotation, localAngularVelocityChange);
        }

        public struct RicochetStepResult
        {
            public bool HitFound;
            public float3 HitPosition;
            public float3 SurfaceNormal;
            public Entity HitEntity;
            public bool IsTerminal;
            public bool IsRicochet;
            public float DistanceTraveled;
        }

#if UNITY_EDITOR || BL_DEBUG
        public static void DrawLinearPidPrediction(ref Drawer drawer, float3 startPos, float3 targetPos,
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
        }

        public static void DrawAngularPidPrediction(ref Drawer drawer, float3 drawPos, quaternion startRot,
            quaternion targetRot, PidTuning tuning, float time)
        {
            var rot = startRot;
            var vel = float3.zero;
            var integral = float3.zero;

            ComputeAngularError(rot, targetRot, out var prevError);

            const float dt = 0.02f;
            var steps = (int)(time / dt);

            var integralMax = tuning.MaxOutput / math.max(tuning.Integral, 0.001f);
            var maxSq = tuning.MaxOutput * tuning.MaxOutput;

            for (var i = 0; i < steps; i++)
            {
                ComputeAngularError(rot, targetRot, out var error);

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
        }
#endif
    }
}