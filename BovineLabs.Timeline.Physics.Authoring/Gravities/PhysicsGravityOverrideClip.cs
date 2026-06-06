using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Gravities
{
    public sealed class PhysicsGravityOverrideClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The gravity scale multiplier. 1 is normal gravity, 0 is zero-G, negative reverses gravity.")]
        public float gravityScale = 1f;

        [Tooltip("If true, restores the original gravity scale when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new PhysicsGravityOverrideAnimated
            {
                AuthoredData = new PhysicsGravityOverrideData
                {
                    GravityScale = gravityScale,
                    RestoreOnExit = restoreOnExit
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}