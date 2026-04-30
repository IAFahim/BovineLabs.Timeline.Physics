using System;
using System.ComponentModel;
using BovineLabs.Core.Authoring.PhysicsStates;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTriggerInstantiateClip))]
    [TrackClipType(typeof(PhysicsTriggerConditionClip))]
    [TrackColor(0.8f, 0.8f, 0.1f)]
    [DisplayName("BovineLabs/Physics/Stateful Trigger")]
    [TrackBindingType(typeof(StatefulTriggerEventAuthoring))]
    public class StatefulTriggerTrack : DOTSTrack
    {
    }
}