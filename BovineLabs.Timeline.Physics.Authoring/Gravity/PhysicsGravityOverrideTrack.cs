using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Gravity
{
    [TrackColor(0.2f, 0.6f, 0.8f)]
    [TrackClipType(typeof(PhysicsGravityOverrideClip))]
    [TrackBindingType(typeof(GameObject))]
    public sealed class PhysicsGravityOverrideTrack : DOTSTrack
    {
    }
}
