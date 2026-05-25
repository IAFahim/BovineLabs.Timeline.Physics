using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTeleportClip))]
    [TrackColor(0.1f, 0.8f, 0.6f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Teleport")]
    public class PhysicsTeleportTrack : DOTSTrack
    {
    }
}