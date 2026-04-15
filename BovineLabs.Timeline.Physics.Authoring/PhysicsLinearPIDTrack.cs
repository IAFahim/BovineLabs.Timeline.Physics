using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsLinearPIDClip))]
    [TrackColor(0.9f, 0.2f, 0.4f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Timeline/Physics/Linear PID")]
    public class PhysicsLinearPIDTrack : DOTSTrack
    {
    }
}