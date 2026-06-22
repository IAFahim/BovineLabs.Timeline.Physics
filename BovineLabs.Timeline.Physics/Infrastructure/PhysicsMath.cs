using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Infrastructure
{
    public static partial class PhysicsMath
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

            var alignment = math.clamp(math.abs(math.dot(currentDir, hit.SurfaceNormal)), 0f, 1f);
            var angleFromNormal = math.acos(alignment);
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
            TryResolveTransform(target, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out position, out rotation);
        }

        public static bool TryResolveTransform(
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
                return false;
            }

            if (localTransformLookup.TryGetComponent(target, out var lt) && !parentLookup.HasComponent(target))
            {
                position = lt.Position;
                rotation = lt.Rotation;
                return true;
            }

            if (localToWorldLookup.TryGetComponent(target, out var ltw))
            {
                position = ltw.Position;
                rotation = new quaternion(math.orthonormalize(new float3x3(ltw.Value)));
                return true;
            }

            position = float3.zero;
            rotation = quaternion.identity;
            return false;
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

        public static float ComputeFalloff(PhysicsTriggerFalloffCurve curve, float distance, float startRadius,
            float endRadius)
        {
            if (curve == PhysicsTriggerFalloffCurve.None) return 1f;
            if (distance > endRadius) return 0f;
            if (distance <= startRadius) return 1f;

            switch (curve)
            {
                case PhysicsTriggerFalloffCurve.Linear:
                {
                    var range = math.max(endRadius - startRadius, 0.001f);
                    return math.saturate(1f - (distance - startRadius) / range);
                }

                case PhysicsTriggerFalloffCurve.InverseSquare:
                {
                    var reference = math.max(startRadius, 0.001f);
                    var ratio = reference / math.max(distance, reference);
                    return ratio * ratio;
                }

                case PhysicsTriggerFalloffCurve.Step:
                    return 1f;

                default:
                    return 1f;
            }
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

            var maxOut = math.max(tuning.MaxOutput, 0f);

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
                var integralMax = maxOut / safeIntegral;
                nextIntegral = math.clamp(nextIntegral, -integralMax, integralMax);
            }

            var derivative = (error - prevError) / deltaTime;

            var rawOutput = tuning.Proportional * error
                            + tuning.Integral * nextIntegral
                            + tuning.Derivative * derivative;

            var magSq = math.lengthsq(rawOutput);
            var maxSq = maxOut * maxOut;

            output = magSq > maxSq ? math.normalize(rawOutput) * maxOut : rawOutput;

            nextState = new PidStateData
            {
                IntegralAccumulator = nextIntegral,
                PreviousError = error,
                CapturedTargetPosition = state.CapturedTargetPosition,
                IsInitialized = true
            };
        }

        public static float3 Reflect(float3 direction, float3 normal)
        {
            return direction - 2f * math.dot(direction, normal) * normal;
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

            var hasTargetTransform = TryResolveTransform(
                targetEntity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var targetPos, out var targetRot);

            if (!hasTargetTransform)
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

            var hasTargetTransform = TryResolveTransform(
                targetEntity, in localTransformLookup, in localToWorldLookup, in parentLookup,
                out var targetPos, out var targetRot);

            if (!hasTargetTransform)
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
    }
}