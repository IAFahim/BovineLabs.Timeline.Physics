namespace BovineLabs.Timeline.Physics.Authoring.Ricochets
{
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine;
    using UnityEngine.Timeline;

    [TrackColor(1.0f, 0.4f, 0.0f)]
    [TrackClipType(typeof(PhysicsRicochetClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Ricochet")]
    public sealed class PhysicsRicochetTrack : DOTSTrack
    {
    }
}
