using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Shapes
{
    [TrackColor(0.2f, 0.6f, 0.8f)]
    [TrackClipType(typeof(PhysicsShapeResizeClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Shape Resize")]
    public sealed class PhysicsShapeResizeTrack : DOTSTrack
    {
    }
}
