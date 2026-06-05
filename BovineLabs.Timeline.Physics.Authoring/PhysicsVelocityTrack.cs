namespace BovineLabs.Timeline.Physics.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using Unity.Physics.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(PhysicsVelocityClip))]
    [TrackColor(0.9f, 0.4f, 0.1f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Velocity")]
    public class PhysicsVelocityTrack : DOTSTrack
    {
    }
}
