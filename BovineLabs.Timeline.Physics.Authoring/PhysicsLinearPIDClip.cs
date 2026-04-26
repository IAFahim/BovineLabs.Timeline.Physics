using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsLinearPIDClip : DOTSClip, ITimelineClipAsset
    {
        // ── Gains (most-tuned first) ──────────────────────────────────────
        [Header("Gains")] public bool uniformAxes = true;

        public PidTuning tuning = new()
        {
            Proportional = new Vector3(10f, 10f, 10f),
            Integral = new Vector3(2f, 2f, 2f),
            Derivative = new Vector3(1f, 1f, 1f),
            MaxOutput = 100f
        };

        // ── Destination ───────────────────────────────────────────────────
        // NOTE: "Tracking Target = Self" means the target position moves WITH the entity.
        // Use a separate reference entity, or set Tracking Target = None + TargetMode = World
        // if you want a fixed world-space destination.
        [Header("Destination")] public Target trackingTarget = Target.Target;

        public PidLinearTargetMode targetMode = PidLinearTargetMode.TargetLocal;
        public Vector3 targetOffset = new(0, 0, 0);

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsLinearPIDAnimated
            {
                AuthoredData = new PhysicsLinearPIDData
                {
                    Tuning = tuning,
                    TrackingTarget = trackingTarget,
                    TargetMode = targetMode,
                    TargetOffset = targetOffset
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}