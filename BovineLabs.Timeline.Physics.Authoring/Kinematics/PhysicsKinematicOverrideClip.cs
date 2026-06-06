using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Kinematics
{
    public sealed class PhysicsKinematicOverrideClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("If true, treats the dynamic body as an infinite-mass kinematic body during this clip.")]
        public bool isKinematic = true;

        [Tooltip("If true, zeroes the linear and angular velocity upon entering the clip.")]
        public bool zeroVelocityOnEnter = true;

        [Tooltip("If true, zeroes gravity factor while the clip is active, restoring it on exit.")]
        public bool zeroGravity = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var builder = new PhysicsKinematicOverrideBuilder
            {
                AuthoredData = new PhysicsKinematicOverrideData
                {
                    IsKinematic = isKinematic,
                    ZeroVelocityOnEnter = zeroVelocityOnEnter,
                    ZeroGravity = zeroGravity
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}