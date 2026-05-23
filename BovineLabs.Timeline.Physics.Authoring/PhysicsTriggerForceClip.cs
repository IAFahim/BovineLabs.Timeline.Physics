using System;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Essence.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public sealed class PhysicsTriggerForceClip : DOTSClip, ITimelineClipAsset
    {
        public StatefulEventState triggerState = StatefulEventState.Enter;
        public PhysicsTriggerForceType forceType = PhysicsTriggerForceType.Radial;
        public PhysicsForceMode mode = PhysicsForceMode.Impulse;

        [Tooltip("Base magnitude. For radial: Positive pulls in (Implosion), Negative pushes out (Explosion).")]
        public float magnitude = 10f;

        [Tooltip("Used only when Force Type is Directional.")]
        public Vector3 direction = Vector3.forward;

        [Header("Radial / Vortex Options")]
        public PhysicsTriggerPositionMode originMode = PhysicsTriggerPositionMode.MatchSelf;

        [Header("Stat Multiplier (Optional)")]
        [Tooltip("If set, multiplies the base magnitude by the target's stat value.")]
        public StatSchemaObject strengthStat;

        public Target readStatFrom = Target.Self;
        public EntityLinkSchema readStatLink;

        [Header("Apply To Target")] public Target applyTo = Target.Target;

        public EntityLinkSchema applyToLink;

        [Header("Filtering")]
        [Tooltip("Ignore collisions with this target (and any colliders sharing its root).")]
        public Target ignoreTarget = Target.Owner;

        [Tooltip("If populated, ONLY colliders matching these Entity Links will trigger the event.")]
        public EntityLinkSchema[] requireLinks = Array.Empty<EntityLinkSchema>();

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            ushort readStatKey = 0;
            if (readStatLink != null && EntityLinkAuthoringUtility.TryGetKey(readStatLink, out var k1))
                readStatKey = k1;

            ushort applyToKey = 0;
            if (applyToLink != null && EntityLinkAuthoringUtility.TryGetKey(applyToLink, out var k2))
                applyToKey = k2;

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerForceData
            {
                EventState = triggerState,
                ForceType = forceType,
                Mode = mode,
                Magnitude = magnitude,
                Direction = direction,
                OriginMode = originMode,
                Strength = new StatStrengthConfig
                {
                    Stat = strengthStat != null ? strengthStat.Key : default,
                    ReadFrom = readStatFrom,
                    LinkKey = readStatKey
                },
                ApplyTo = applyTo,
                ApplyToLinkKey = applyToKey
            });

            // Bake the filter data
            BlobAssetReference<PhysicsTriggerLinkBlob> filterBlob = default;
            if (requireLinks != null && requireLinks.Length > 0)
            {
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<PhysicsTriggerLinkBlob>();
                var array = builder.Allocate(ref root.ValidLinkKeys, requireLinks.Length);

                int validCount = 0;
                for (int i = 0; i < requireLinks.Length; i++)
                {
                    if (EntityLinkAuthoringUtility.TryGetKey(requireLinks[i], out var reqKey))
                        array[validCount++] = reqKey;
                }

                if (validCount > 0)
                {
                    filterBlob = builder.CreateBlobAssetReference<PhysicsTriggerLinkBlob>(Allocator.Persistent);
                    context.Baker.AddBlobAsset(ref filterBlob, out _);
                }

                builder.Dispose();
            }

            context.Baker.AddComponent(clipEntity, new PhysicsTriggerFilterData
            {
                IgnoreTarget = ignoreTarget,
                LinkFilterBlob = filterBlob
            });

            base.Bake(clipEntity, context);
        }
    }
}