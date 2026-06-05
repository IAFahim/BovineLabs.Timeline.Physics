namespace BovineLabs.Timeline.Physics.Authoring.Gravities
{
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine;
    using UnityEngine.Timeline;

    [TrackColor(0.2f, 0.6f, 0.8f)]
    [TrackClipType(typeof(PhysicsGravityOverrideClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Gravity Override")]
    public sealed class PhysicsGravityOverrideTrack : DOTSTrack
    {
    }
}
