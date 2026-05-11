using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsAngularPIDClip))]
    [TrackColor(0.9f, 0.4f, 0.4f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Angular PID")]
    public class PhysicsAngularPIDTrack : DOTSTrack
    {
    }
}