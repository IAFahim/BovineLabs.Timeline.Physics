namespace BovineLabs.Timeline.Physics.Authoring.PIDs
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using Unity.Physics.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(PhysicsLinearPIDClip))]
    [TrackColor(0.9f, 0.2f, 0.4f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Linear PID")]
    public class PhysicsLinearPIDTrack : DOTSTrack
    {
    }
}
