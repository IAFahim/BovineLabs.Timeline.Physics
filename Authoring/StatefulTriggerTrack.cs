using UnityEngine;
using BovineLabs.Timeline.Authoring;
using System;
using System.ComponentModel;
using BovineLabs.Core.Authoring.PhysicsStates;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using BovineLabs.Reaction.Authoring.Core;
using UnityEditor;
#endif

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTriggerInstantiateClip))]
    [TrackColor(0.8f, 0.8f, 0.1f)]
    [DisplayName("BovineLabs/Timeline/Physics/Stateful Trigger")]
    [TrackBindingType(typeof(StatefulTriggerEventAuthoring))]
    public class StatefulTriggerTrack : DOTSTrack
    {
#if UNITY_EDITOR
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            EnsureTargetsAuthoring(director);
            base.GatherProperties(director, driver);
        }

        private void EnsureTargetsAuthoring(PlayableDirector director)
        {
            if (director == null)
            {
                return;
            }

            var binding = director.GetGenericBinding(this);
            var gameObject = binding switch
            {
                GameObject targetGameObject => targetGameObject,
                UnityEngine.Component targetComponent => targetComponent.gameObject,
                _ => null,
            };

            if (gameObject == null || gameObject.GetComponent<TargetsAuthoring>() != null)
            {
                return;
            }

            Undo.AddComponent<TargetsAuthoring>(gameObject);
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}
