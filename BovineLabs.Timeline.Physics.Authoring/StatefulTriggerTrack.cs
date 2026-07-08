using System;
using System.ComponentModel;
using BovineLabs.Nerve.Authoring.PhysicsStates;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTriggerInstantiateClip))]
    [TrackClipType(typeof(PhysicsTriggerConditionClip))]
    [TrackClipType(typeof(PhysicsTriggerForceClip))]
    [TrackClipType(typeof(PhysicsKnockbackClip))]
    [TrackClipType(typeof(PhysicsThrustClip))]
    [TrackClipType(typeof(PhysicsVortexClip))]
    [TrackClipType(typeof(PhysicsBreakForceClip))]
    [TrackClipType(typeof(PhysicsTriggerQueryClip))]
    [TrackClipType(typeof(PhysicsTargetSelectClip))]
    [TrackClipType(typeof(PhysicsDirectionalQueryClip))]
    [TrackClipType(typeof(PhysicsAoEQueryClip))]
    [TrackColor(0.8f, 0.8f, 0.1f)]
    [DisplayName("BovineLabs/Physics/Stateful Trigger")]
    [TrackBindingType(typeof(StatefulTriggerEventAuthoring))]
    public class StatefulTriggerTrack : DOTSTrack
    {
    }
}