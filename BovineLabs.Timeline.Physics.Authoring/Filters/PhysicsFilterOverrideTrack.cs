using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Filters
{
    [TrackColor(0.8f, 0.2f, 0.2f)]
    [TrackClipType(typeof(PhysicsFilterOverrideClip))]
    [TrackBindingType(typeof(GameObject))]
    public sealed class PhysicsFilterOverrideTrack : DOTSTrack
    {
    }
}
