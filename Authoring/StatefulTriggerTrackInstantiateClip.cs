using BovineLabs.Timeline.Authoring;
using BovineLabs.Core.PhysicsStates;
using BovineLabs.Timeline.Instantiate;
using BovineLabs.Timeline.Physics;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Timeline;
using BovineLabs.Timeline.Instantiate;
using BovineLabs.Core.PhysicsStates;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class StatefulTriggerTrackInstantiateClip : DOTSClip, ITimelineClipAsset
    {
        public GameObject prefab;

        public ParentTransformConfig parentTransformConfig;

        [Tooltip("Instantiate when collision/trigger state matches:")]
        public StatefulEventState eventState = StatefulEventState.Enter;

        public bool setTarget = true;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.None;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new InstantiateConfigComponent
            {
                Prefab = context.Baker.GetEntity(prefab, TransformUsageFlags.None),
                ParentTransformConfig = parentTransformConfig
            });
            context.Baker.AddComponent(clipEntity, new OnClipActiveStatefulInstantiateTag());
            context.Baker.AddComponent(clipEntity, new StatefulEventStateConfig
            {
                Value = eventState
            });
            base.Bake(clipEntity, context);
        }
    }
}
