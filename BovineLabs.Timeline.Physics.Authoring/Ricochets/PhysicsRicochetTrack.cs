using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Ricochets
{
    [TrackColor(1.0f, 0.4f, 0.0f)]
    [TrackClipType(typeof(PhysicsRicochetClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Ricochet")]
    public sealed class PhysicsRicochetTrack : DOTSTrack
    {
    }
}