using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsDragClip))]
    [TrackColor(0.2f, 0.4f, 0.8f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Timeline/Physics/Drag (Brakes)")]
    public class PhysicsDragTrack : DOTSTrack
    {
    }
}