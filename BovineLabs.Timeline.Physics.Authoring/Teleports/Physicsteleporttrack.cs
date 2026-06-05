using System;
using System.ComponentModel;
using BovineLabs.Reaction.Authoring.Core;
using BovineLabs.Timeline.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Teleports
{
    [Serializable]
    [TrackClipType(typeof(PhysicsTeleportClip))]
    [TrackColor(0.1f, 0.8f, 0.6f)]
    [TrackBindingType(typeof(TargetsAuthoring))]
    [DisplayName("BovineLabs/Physics/Teleport")]
    public class PhysicsTeleportTrack : DOTSTrack
    {
    }
}