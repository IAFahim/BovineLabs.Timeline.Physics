using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;
using BoxCollider = Unity.Physics.BoxCollider;
using CapsuleCollider = Unity.Physics.CapsuleCollider;
using SphereCollider = Unity.Physics.SphereCollider;
using Collider = Unity.Physics.Collider;

namespace BovineLabs.Timeline.Physics.Authoring.Shapes
{
    public sealed class PhysicsShapeSwapClip : DOTSClip, ITimelineClipAsset
    {
        public enum ShapeKind
        {
            Sphere,
            Box,
            Capsule,
            Cylinder,
        }

        [Tooltip("The shape the collider is replaced with while this clip is active.")]
        public ShapeKind kind = ShapeKind.Sphere;

        [Tooltip("Local-space center offset of the swapped shape.")]
        public Vector3 center = Vector3.zero;

        [Tooltip("Radius — used by Sphere / Capsule / Cylinder.")]
        public float radius = 0.5f;

        [Tooltip("Full height (tip to tip) — used by Capsule / Cylinder.")]
        public float height = 2f;

        [Tooltip("Box half-unaware full size — used by Box.")]
        public Vector3 boxSize = Vector3.one;

        [Tooltip("BelongsTo collision mask of the swapped collider.")]
        public PhysicsCategoryTags belongsTo = new() { Value = ~0u };

        [Tooltip("CollidesWith collision mask of the swapped collider.")]
        public PhysicsCategoryTags collidesWith = new() { Value = ~0u };

        [Tooltip("Restore the original collider when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var filter = new CollisionFilter
            {
                BelongsTo = belongsTo.Value == 0 ? ~0u : belongsTo.Value,
                CollidesWith = collidesWith.Value == 0 ? ~0u : collidesWith.Value,
                GroupIndex = 0,
            };

            var blob = BuildCollider(filter);
            context.Baker.AddBlobAsset(ref blob, out _);

            var builder = new PhysicsShapeSwapBuilder
            {
                AuthoredData = new PhysicsShapeSwapData
                {
                    NewCollider = blob,
                    RestoreOnExit = restoreOnExit,
                },
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }

        private BlobAssetReference<Collider> BuildCollider(CollisionFilter filter)
        {
            var c = (float3)center;
            var r = math.max(radius, 1e-3f);

            switch (kind)
            {
                case ShapeKind.Box:
                {
                    var size = math.max((float3)boxSize, 1e-3f);
                    return BoxCollider.Create(new BoxGeometry
                    {
                        Center = c,
                        Size = size,
                        Orientation = quaternion.identity,
                        BevelRadius = math.min(0.05f, math.cmin(size) * 0.5f),
                    }, filter);
                }

                case ShapeKind.Capsule:
                {
                    var half = math.max(height * 0.5f - r, 0f); // segment half-length (tip-to-tip incl. radius)
                    return CapsuleCollider.Create(new CapsuleGeometry
                    {
                        Vertex0 = c - new float3(0f, half, 0f),
                        Vertex1 = c + new float3(0f, half, 0f),
                        Radius = r,
                    }, filter);
                }

                case ShapeKind.Cylinder:
                {
                    var h = math.max(height, 2e-3f);
                    return CylinderCollider.Create(new CylinderGeometry
                    {
                        Center = c,
                        Orientation = quaternion.identity,
                        Height = h,
                        Radius = r,
                        BevelRadius = math.min(0.05f, math.min(r, h * 0.5f)),
                        SideCount = 20,
                    }, filter);
                }

                default:
                    return SphereCollider.Create(new SphereGeometry { Center = c, Radius = r }, filter);
            }
        }
    }
}
