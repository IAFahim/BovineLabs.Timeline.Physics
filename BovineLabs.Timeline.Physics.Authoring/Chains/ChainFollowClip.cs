using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Chains;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Chains
{
    public class ChainFollowClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Physics Blend Weight")] [Range(0f, 1f)]
        public float positionStrength = 1f;

        [Range(0f, 1f)] public float orientationStrength = 1f;

        [Header("Crispness (seconds to halve the gap)")] [Min(0.001f)]
        public float positionHalflife = 0.05f;

        [Min(0.001f)] public float orientationHalflife = 0.05f;

        [Header("Limits")] [Min(0f)] public float maxLinearSpeed = 60f;
        [Min(0f)] public float maxAngularSpeed = 50f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            var builder = new ChainFollowBuilder
            {
                AuthoredData = new ChainFollowData
                {
                    PositionStrength = positionStrength,
                    OrientationStrength = orientationStrength,
                    PositionHalflife = positionHalflife,
                    OrientationHalflife = orientationHalflife,
                    MaxLinearSpeed = maxLinearSpeed,
                    MaxAngularSpeed = maxAngularSpeed
                }
            };
            builder.ApplyTo(ref commands);
            base.Bake(clipEntity, context);
        }
    }
}