using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    /// <summary>
    /// Scatter (explosion/spray) force — the random-direction split of <see cref="PhysicsForceClip"/>. Kicks the body
    /// in a random direction each fire: a full sphere, or a cone you aim. Same downstream force machinery as
    /// PhysicsForceClip (impulse/continuous, channel, velocity reset, angular, strength) — just a focused inspector
    /// with only the scatter knobs. For a directed push (fixed vector / toward-target / along-velocity) use
    /// PhysicsForceClip instead.
    /// </summary>
    public class PhysicsScatterForceClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema readStatLink;

        [Tooltip("Impulse mode applies force exactly once per clip activation and ignores Looping.")]
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Intent = a normal force your own drag/clamp/reset clips can shape. " +
                 "External = a knockback that ignores the body's own braking and decays on its own.")]
        public MotionChannel channel = MotionChannel.Intent;

        [Tooltip("On = directional spray inside a cone (aimed by Space + the cone angles). Off = full-sphere scatter " +
                 "(cone angles ignored).")]
        public bool cone = true;

        public float magnitude = 10f;

        [Tooltip("Frame the cone is aimed in. Self = the body's own orientation; sphere scatter ignores this.")]
        public Target space = Target.Self;

        [Header("Cone (degrees, ignored for sphere)")]
        [Tooltip("Azimuth 0 points along +Z of the Space frame; 180 points behind it.")]
        public float coneAzimuthCenter;

        [Range(0f, 180f)] public float coneAzimuthHalfRange = 30f;

        public float coneElevationCenter;

        [Range(0f, 89f)] public float coneElevationHalfRange = 15f;

        [Header("Randomness")]
        [Tooltip("Offsets this body's random stream. 0 is valid; entity identity already decorrelates bodies.")]
        public uint seed;

        [Tooltip("Sample the random direction once per clip activation and hold it. Disable to re-roll every fire.")]
        public bool latchDirection = true;

        [Header("Velocity Reset")]
        [Tooltip("Zeroes the body's velocity once per clip activation, immediately before this force lands.")]
        public VelocityResetFlags resetVelocityOnFire = VelocityResetFlags.None;

        [Header("Angular & Multipliers")] public Vector3 angularForce;

        public StatSchemaObject strengthStat;
        public Target readStatFrom = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var builder = new PhysicsForceBuilder
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    Channel = channel,
                    DirectionMode = cone ? PhysicsForceDirectionMode.RandomCone : PhysicsForceDirectionMode.RandomSphere,
                    Space = space,
                    Magnitude = magnitude,
                    ConeAzimuthCenter = math.radians(coneAzimuthCenter),
                    ConeAzimuthHalfRange = math.radians(coneAzimuthHalfRange),
                    ConeElevationCenter = math.radians(coneElevationCenter),
                    ConeElevationHalfRange = math.radians(coneElevationHalfRange),
                    Seed = seed,
                    LatchDirection = latchDirection,
                    ResetVelocityOnFire = resetVelocityOnFire,
                    Angular = angularForce,
                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    },
                },
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}
