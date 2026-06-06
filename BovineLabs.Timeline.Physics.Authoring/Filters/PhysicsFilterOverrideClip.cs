using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Filters
{
    public sealed class PhysicsFilterOverrideClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The new BelongsTo collision mask (as an integer bitmask).")]
        public uint belongsToOverride = 0xFFFFFFFF;

        [Tooltip("The new CollidesWith collision mask (as an integer bitmask).")]
        public uint collidesWithOverride = 0xFFFFFFFF;

        [Tooltip("If true, restores the original collision mask when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            commands.AddComponent(new PhysicsFilterOverrideAnimated
            {
                AuthoredData = new PhysicsFilterOverrideData
                {
                    BelongsToOverride = belongsToOverride,
                    CollidesWithOverride = collidesWithOverride,
                    RestoreOnExit = restoreOnExit
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}