using BovineLabs.Core.PhysicsStates;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using BoxCollider = Unity.Physics.BoxCollider;
using CapsuleCollider = Unity.Physics.CapsuleCollider;
using SphereCollider = Unity.Physics.SphereCollider;

namespace BovineLabs.Timeline.Physics.Authoring
{
    using Collider = Unity.Physics.Collider;

    [DisallowMultipleComponent]
    public class SweptTriggerSourceAuthoring : MonoBehaviour
    {
        [Tooltip(
            "Sub-steps per frame interpolating the prev->cur transform (>= 1). The system already auto-densifies " +
            "with angular speed; raise this only as a floor for extreme spins. 1 is fine for most attacks.")]
        [Min(1)]
        public int subSteps = 1;

        private class Baker : Baker<SweptTriggerSourceAuthoring>
        {
            public override void Bake(SweptTriggerSourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                var shape = GetComponent<PhysicsShapeAuthoring>();
                var blob = BuildColliderBlob(shape);
                AddBlobAsset(ref blob, out _);

                var aabb = blob.Value.CalculateAabb();
                var extents = aabb.Max - aabb.Min;
                var tipRadius = math.length(math.max(math.abs(aabb.Min), math.abs(aabb.Max)));
                var thickness = math.max(1e-3f, math.cmin(extents) * 0.5f);

                AddComponent(entity, new SweptTriggerConfig
                {
                    Collider = blob,
                    SubSteps = math.max(1, authoring.subSteps),
                    TipRadius = tipRadius,
                    Thickness = thickness,
                    DebugCenter = aabb.Center,
                    DebugExtents = extents
                });
                AddComponent(entity, default(SweptTriggerState));
                AddBuffer<SweptTriggerHit>(entity);

                AddBuffer<StatefulTriggerEvent>(entity);
            }

            private BlobAssetReference<Collider> BuildColliderBlob(PhysicsShapeAuthoring shape)
            {
                if (shape == null)
                {
                    Debug.LogWarning("SweptTriggerSourceAuthoring: no PhysicsShapeAuthoring on the object. Add one " +
                                     "(disabled) to define the swept volume. Falling back to a small default capsule.");
                    return DefaultCapsule();
                }

                if (shape.enabled)
                    Debug.LogWarning(
                        $"SweptTriggerSourceAuthoring on '{shape.name}': the PhysicsShapeAuthoring is ENABLED, " +
                        "so it ALSO bakes into a real collider. UNTICK its component checkbox — the swept " +
                        "source reads its shape but it must not be a real physics body.");

                var cw = shape.CollidesWith.Value;
                var filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = cw == 0 ? ~0u : cw, GroupIndex = 0 };

                switch (shape.ShapeType)
                {
                    case ShapeType.Box:
                        return BoxCollider.Create(shape.GetBoxProperties(), filter);

                    case ShapeType.Capsule:
                        return CapsuleCollider.Create(shape.GetCapsuleProperties().ToRuntime(), filter);

                    case ShapeType.Sphere:
                        return SphereCollider.Create(shape.GetSphereProperties(out var _), filter);

                    case ShapeType.Cylinder:
                        return CylinderCollider.Create(shape.GetCylinderProperties(), filter);

                    case ShapeType.ConvexHull:
                    {
                        var points = new NativeList<float3>(Allocator.Temp);
                        shape.GetConvexHullProperties(points);
                        var blob = ConvexCollider.Create(
                            points.AsArray(), ConvexHullGenerationParameters.Default, filter);
                        points.Dispose();
                        return blob;
                    }

                    default:
                        Debug.LogWarning(
                            $"SweptTriggerSourceAuthoring on '{shape.name}': shape type '{shape.ShapeType}' is " +
                            "not supported for swept triggers (use Box/Capsule/Sphere/Cylinder/Convex Hull). " +
                            "Falling back to its bounding box.");
                        return BoxCollider.Create(shape.GetBoxProperties(), filter);
                }
            }

            private static BlobAssetReference<Collider> DefaultCapsule()
            {
                var geom = new CapsuleGeometry
                {
                    Vertex0 = new float3(0f, -0.4f, 0f),
                    Vertex1 = new float3(0f, 0.4f, 0f),
                    Radius = 0.06f
                };
                var filter = new CollisionFilter { BelongsTo = ~0u, CollidesWith = ~0u, GroupIndex = 0 };
                return CapsuleCollider.Create(geom, filter);
            }
        }
    }
}