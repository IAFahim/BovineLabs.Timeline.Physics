using BovineLabs.Core.Iterators;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Teleports
{
    public static class TeleportPlacement
    {
        public static LocalTransform ComputeLocalTransform(
            in TeleportFrame frame,
            in PhysicsTeleportData data,
            float3 landing,
            float localScale,
            in UnsafeComponentLookup<LocalToWorld> transforms,
            in ComponentLookup<Parent> parents)
        {
            if (!parents.HasComponent(frame.TeleportedEntity))
                return ComputeLocalTransform(in frame, in data, landing, localScale, false, default);

            var parent = parents[frame.TeleportedEntity];
            if (!transforms.TryGetComponent(parent.Value, out var parentLtw))
                return ComputeLocalTransform(in frame, in data, landing, localScale, false, default);

            return ComputeLocalTransform(in frame, in data, landing, localScale, true, in parentLtw);
        }

        public static LocalTransform ComputeLocalTransform(
            in TeleportFrame frame,
            in PhysicsTeleportData data,
            float3 landing,
            float localScale,
            bool hasParentTransform,
            in LocalToWorld parentLocalToWorld)
        {
            TeleportMath.ComputeFacingRotation(
                data.FacingMode, landing, frame.FacingPosition,
                frame.TeleportedRotation, frame.FacingRotation, out var facing);

            var worldTransform = LocalTransform.FromPositionRotation(landing, facing);
            worldTransform.Scale = localScale;

            if (!hasParentTransform || math.abs(math.determinant(parentLocalToWorld.Value)) < math.EPSILON)
                return worldTransform;

            var parentPosition = parentLocalToWorld.Value.c3.xyz;
            var parentRotation = new quaternion(math.orthonormalize(new float3x3(parentLocalToWorld.Value)));
            var inverseParentRotation = math.conjugate(parentRotation);
            var parentScale = math.length(parentLocalToWorld.Value.c0.xyz);

            var localPosition = math.mul(inverseParentRotation, worldTransform.Position - parentPosition);
            if (parentScale > math.EPSILON)
                localPosition /= parentScale;
            var localRotation = math.mul(inverseParentRotation, worldTransform.Rotation);

            var localTransform = LocalTransform.FromPositionRotation(localPosition, localRotation);
            localTransform.Scale = localScale;
            return localTransform;
        }
    }
}