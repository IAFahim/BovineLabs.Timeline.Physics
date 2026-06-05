namespace BovineLabs.Timeline.Physics.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using Unity.Physics.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(PhysicsForceClip))]
    [TrackColor(0.8f, 0.4f, 0.2f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Force")]
    public class PhysicsForceTrack : DOTSTrack
    {
    }
}
