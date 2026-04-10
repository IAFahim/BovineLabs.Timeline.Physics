using BovineLabs.Timeline.Authoring;
using System;
using System.ComponentModel;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsPIDClip))]
    [TrackColor(0.9f, 0.2f, 0.4f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Timeline/Physics/PID Controller")]
    public class PhysicsPIDTrack : DOTSTrack
    {
    }
}