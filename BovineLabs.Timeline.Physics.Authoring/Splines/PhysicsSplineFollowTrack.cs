using System;
using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Splines
{
    [Serializable]
    [TrackClipType(typeof(PhysicsSplineFollowClip))]
    [TrackColor(0.2f, 0.7f, 0.7f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Physics/Spline Follow")]
    public class PhysicsSplineFollowTrack : DOTSTrack
    {
    }
}
