namespace BovineLabs.Timeline.Physics.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using Unity.Physics.Authoring;
    using UnityEngine.Timeline;

    [Serializable]
    [TrackClipType(typeof(PhysicsDragClip))]
    [TrackColor(0.2f, 0.4f, 0.8f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Drag (Brakes)")]
    public class PhysicsDragTrack : DOTSTrack
    {
    }
}
