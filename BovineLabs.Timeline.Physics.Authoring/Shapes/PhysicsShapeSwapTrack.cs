using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Shapes
{
    [TrackColor(0.2f, 0.4f, 0.8f)]
    [TrackClipType(typeof(PhysicsShapeSwapClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Shape Swap")]
    public sealed class PhysicsShapeSwapTrack : DOTSTrack
    {
    }
}
