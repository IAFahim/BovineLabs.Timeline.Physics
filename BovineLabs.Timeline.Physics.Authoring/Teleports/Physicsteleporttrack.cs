using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTeleportClip))]
    [TrackColor(0.1f, 0.8f, 0.6f)]
    [TrackBindingType(typeof(TargetsAuthoring))] // Changed from PhysicsBodyAuthoring
    [DisplayName("BovineLabs/Physics/Teleport")]
    public class PhysicsTeleportTrack : DOTSTrack
    {
    }
}