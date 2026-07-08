using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Builders;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsForceClip : DOTSClip, ITimelineClipAsset
    {
        public EntityLinkSchema directionTargetLink;
        public EntityLinkSchema readStatLink;

        [Tooltip("Impulse mode applies force exactly once per clip activation and ignores Looping.")]
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Intent = a normal force your own drag/clamp/reset clips can shape (locomotion, dashes, thrust). " +
                 "External = a knockback that ignores the body's own braking and decays on its own — use this for " +
                 "hits/launches that must land even while the body is air-braking.")]
        public MotionChannel channel = MotionChannel.Intent;

        [Tooltip("Directed push only (fixed vector / toward-away target / along-against velocity). " +
                 "For random sphere/cone scatter use PhysicsScatterForceClip instead.")]
        public PhysicsForceDirectionMode directionMode = PhysicsForceDirectionMode.FixedVector;

        [Header("Fixed Vector")]
        [Tooltip("Full push vector: direction AND strength baked into one. Used ONLY when Direction Mode = FixedVector. " +
                 "Ignored by the Toward/Away target modes (they use Magnitude instead).")]
        public Vector3 linearForce = new(0, 0, 0);

        public Target space = Target.Self;

        [Header("Toward Target")]
        [Tooltip("Push STRENGTH only (a scalar). Direction comes from the target/velocity at runtime. " +
                 "Used ONLY when Direction Mode = Toward/AwayFromTarget. Ignored by FixedVector (it uses Linear Force instead).")]
        [FormerlySerializedAs("magnitude")]
        public float directionStrength = 10f;

        public Target directionTarget = Target.Target;

        [Header("Direction Latch")]
        [Tooltip("Sample velocity-relative directions once per clip activation and hold them. " +
                 "Disable to re-evaluate every fire.")]
        public bool latchDirection = true;

        [Header("Velocity Reset")]
        [Tooltip("Zeroes the body's velocity once per clip activation, immediately before this force lands. " +
                 "Use Linear for dashes that must always travel the same distance.")]
        public VelocityResetFlags resetVelocityOnFire = VelocityResetFlags.None;

        [Header("Angular & Multipliers")] public Vector3 angularForce;

        public StatSchemaObject strengthStat;
        public Target readStatFrom = Target.Self;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            // Scatter was split into PhysicsScatterForceClip; a directed-force clip left on a random mode has no cone
            // params, so fall back to a fixed vector rather than bake a degenerate zero-cone.
            var resolvedMode = directionMode;
            if (resolvedMode is PhysicsForceDirectionMode.RandomSphere or PhysicsForceDirectionMode.RandomCone)
            {
                Debug.LogWarning($"[{nameof(PhysicsForceClip)}] {name}: '{directionMode}' scatter moved to " +
                    $"PhysicsScatterForceClip; baking as FixedVector. Recreate this clip as a Scatter Force clip.");
                resolvedMode = PhysicsForceDirectionMode.FixedVector;
            }

            var builder = new PhysicsForceBuilder
            {
                AuthoredData = new PhysicsForceData
                {
                    Mode = mode,
                    Channel = channel,
                    DirectionMode = resolvedMode,
                    Linear = linearForce,
                    Space = space,
                    Magnitude = directionStrength,
                    DirectionTarget = EntityLinkAuthoringUtility.BakeRef(context.Baker, directionTargetLink, directionTarget),
                    LatchDirection = latchDirection,
                    ResetVelocityOnFire = resetVelocityOnFire,
                    Angular = angularForce,
                    Strength = new StatSource
                    {
                        Stat = strengthStat != null ? strengthStat.Key : default,
                        Link = EntityLinkAuthoringUtility.BakeRef(context.Baker, readStatLink, readStatFrom),
                    }
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}