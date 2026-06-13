using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Filters
{
    public sealed class PhysicsFilterOverrideClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("The new BelongsTo collision mask (as an integer bitmask).")]
        public PhysicsCategoryTags belongsToOverride;

        [Tooltip("The new CollidesWith collision mask (as an integer bitmask).")]
        public PhysicsCategoryTags collidesWithOverride;

        [Tooltip("If true, restores the original collision mask when the clip ends.")]
        public bool restoreOnExit = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var builder = new PhysicsFilterOverrideBuilder
            {
                AuthoredData = new PhysicsFilterOverrideData
                {
                    BelongsToOverride = belongsToOverride.Value,
                    CollidesWithOverride = collidesWithOverride.Value,
                    RestoreOnExit = restoreOnExit
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}