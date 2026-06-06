using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.VelocityClamps
{
    public sealed class PhysicsVelocityClampClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Maximum linear speed. Set to negative to ignore.")]
        public float maxLinearSpeed = 10f;

        [Tooltip("Maximum angular speed (radians per second). Set to negative to ignore.")]
        public float maxAngularSpeed = -1f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var builder = new PhysicsVelocityClampBuilder
            {
                AuthoredData = new PhysicsVelocityClampData
                {
                    MaxLinearSpeed = maxLinearSpeed,
                    MaxAngularSpeed = maxAngularSpeed
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}