using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Kinematic
{
    [TrackColor(0.5f, 0.5f, 0.5f)]
    [TrackClipType(typeof(PhysicsKinematicOverrideClip))]
    [TrackBindingType(typeof(GameObject))]
    public sealed class PhysicsKinematicOverrideTrack : DOTSTrack
    {
    }
}
