using System;
using System.ComponentModel;
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
    [TrackColor(0.95f, 0.45f, 0.1f)]
    [DisplayName("BovineLabs/Physics/Swept Trigger")]
    [TrackBindingType(typeof(SweptTriggerSourceAuthoring))]
    public class SweptTriggerTrack : DOTSTrack
    {
    }
}