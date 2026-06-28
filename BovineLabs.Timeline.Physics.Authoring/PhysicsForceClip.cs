using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsForceClip : DOTSClip, ITimelineClipAsset
    {
        [Tooltip("Impulse mode applies force exactly once per clip activation and ignores Looping.")]
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Intent = a normal force your own drag/clamp/reset clips can shape (locomotion, dashes, thrust). " +
                 "External = a knockback that ignores the body's own braking and decays on its own — use this for " +
                 "hits/launches that must land even while the body is air-braking.")]
        public MotionChannel channel = MotionChannel.Intent;

        public PhysicsForceDirectionMode directionMode = PhysicsForceDirectionMode.FixedVector;

        [Header("Fixed Vector")] public Vector3 linearForce = new(0, 0, 0);

        public Target space = Target.Self;

        [Header("Toward Target")] public float magnitude = 10f;

        public Target directionTarget = Target.Target;
        public EntityLinkSchema directionTargetLink;

        [Header("Random Cone (degrees)")]
        [Tooltip("Azimuth 0 points along +Z of the Space frame; 180 points behind it.")]
        public float coneAzimuthCenter;

        [Range(0f, 180f)] public float coneAzimuthHalfRange = 30f;

        public float coneElevationCenter;

        [Range(0f, 89f)] public float coneElevationHalfRange = 15f;

        [Header("Randomness")]
        [Tooltip("Offsets this body's random stream. 0 is valid; entity identity already decorrelates bodies.")]
        public uint seed;

        [Tooltip("Sample random/velocity-relative directions once per clip activation and hold them. " +
                 "Disable to re-evaluate every fire.")]
        public bool latchDirection = true;

        [Header("Velocity Reset")]
        [Tooltip("Zeroes the body's velocity once per clip activation, immediately before this force lands. " +
                 "Use Linear for dashes that must always travel the same distance.")]
        public VelocityResetFlags resetVelocityOnFire = VelocityResetFlags.None;

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

            var builder = new PhysicsForceBuilder
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    Channel = channel,
                    DirectionMode = directionMode,
                    Linear = linearForce,
                    Space = space,
                    Magnitude = magnitude,
                    DirectionTarget = directionTarget,
                    DirectionTargetLinkKey = dirLinkKey,
                    ConeAzimuthCenter = math.radians(coneAzimuthCenter),
                    ConeAzimuthHalfRange = math.radians(coneAzimuthHalfRange),
                    ConeElevationCenter = math.radians(coneElevationCenter),
                    ConeElevationHalfRange = math.radians(coneElevationHalfRange),
                    Seed = seed,
                    LatchDirection = latchDirection,
                    ResetVelocityOnFire = resetVelocityOnFire,
                    Angular = angularForce,
                    Strength = new StatStrengthConfig
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        ReadFrom = readStatFrom,
                        LinkKey = readStatKey
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}