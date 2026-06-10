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

            if (!hasParentTransform)
                return worldTransform;

            var worldMatrix = float4x4.TRS(worldTransform.Position, worldTransform.Rotation, 1f);
            var localMatrix = math.mul(math.inverse(parentLocalToWorld.Value), worldMatrix);
            var localTransform = LocalTransform.FromMatrix(localMatrix);
            localTransform.Scale = localScale;
            return localTransform;
        }
    }
}
