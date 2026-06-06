using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsForceClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Impulse mode applies force exactly once per clip activation and ignores Looping.")]
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        public PhysicsForceDirectionMode directionMode = PhysicsForceDirectionMode.FixedVector;

        [Header("Fixed Vector")] public Vector3 linearForce = new(0, 0, 0);

        public Target space = Target.Self;

        [Header("Toward Target")] public float magnitude = 10f;

        public Target directionTarget = Target.Target;
        public EntityLinkSchema directionTargetLink;

        [Header("Angular & Multipliers")] public Vector3 angularForce;

        public StatSchemaObject strengthStat;
        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            ushort dirLinkKey = 0;
            if (directionTargetLink != null && EntityLinkAuthoringUtility.TryGetKey(directionTargetLink, out var k2))
                dirLinkKey = k2;

            commands.AddComponent(new PhysicsForceAnimated
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    DirectionMode = directionMode,
                    Linear = linearForce,
                    Space = space,
                    Magnitude = magnitude,
                    DirectionTarget = directionTarget,
                    DirectionTargetLinkKey = dirLinkKey,
                    Angular = angularForce,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}