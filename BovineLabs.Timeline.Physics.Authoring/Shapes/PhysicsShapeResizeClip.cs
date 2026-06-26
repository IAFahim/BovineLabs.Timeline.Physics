using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Shapes
{
    public sealed class PhysicsShapeResizeClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Uniform scale on the collider size while active (1 = unchanged, 2 = double). Mutates the PHYSICS " +
                 "collider only, not the renderer. Sphere/Capsule/Cylinder/Box supported; convex/mesh skipped.")]
        public float scale = 2f;

        [Tooltip("Use a per-axis scale instead of uniform. Radius-based shapes (sphere/capsule/cylinder) take their " +
                 "radius from the X axis.")]
        public bool nonUniform;

        [Tooltip("Per-axis scale used when 'Non Uniform' is on.")]
        public Vector3 scaleAxes = new(2f, 2f, 2f);

        [Tooltip("Restore the original collider size when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var builder = new PhysicsShapeResizeBuilder
            {
                AuthoredData = new PhysicsShapeResizeData
                {
                    Scale = nonUniform ? (float3)scaleAxes : new float3(scale),
                    RestoreOnExit = restoreOnExit
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}
