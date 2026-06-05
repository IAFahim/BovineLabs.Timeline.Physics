using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.VelocityClamps
{
    [TrackColor(0.8f, 0.6f, 0.2f)]
    [TrackClipType(typeof(PhysicsVelocityClampClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Velocity Clamp")]
    public sealed class PhysicsVelocityClampTrack : DOTSTrack
    {
    }
}