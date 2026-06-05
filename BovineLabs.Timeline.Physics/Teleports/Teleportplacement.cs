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
            in UnsafeComponentLookup<LocalToWorld> transforms,
            in ComponentLookup<Parent> parents)
        {
            TeleportMath.ComputeFacingRotation(
                data.FacingMode, landing, frame.FacingPosition,
                frame.TeleportedRotation, frame.FacingRotation, out var facing);

            var worldTransform = LocalTransform.FromPositionRotation(landing, facing);

            if (!parents.HasComponent(frame.TeleportedEntity))
                return worldTransform;

            var parent = parents[frame.TeleportedEntity];
            if (!transforms.TryGetComponent(parent.Value, out var parentLtw))
                return worldTransform;

            var worldMatrix = float4x4.TRS(worldTransform.Position, worldTransform.Rotation, 1f);
            var localMatrix = math.mul(math.inverse(parentLtw.Value), worldMatrix);
            return LocalTransform.FromMatrix(localMatrix);
        }
    }
}