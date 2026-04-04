using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsTriggerInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        public GameObject prefab;
        public StatefulEventState eventState = StatefulEventState.Enter;
        public bool snapToTransform = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsTriggerInstantiateData
            {
                Prefab = context.Baker.GetEntity(prefab, TransformUsageFlags.None),
                EventState = eventState,
                SnapToTransform = snapToTransform
            });
            base.Bake(clipEntity, context);
        }
    }
}