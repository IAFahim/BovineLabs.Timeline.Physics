using System.ComponentModel;
using BovineLabs.Timeline.Authoring;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Kinematics
{
    [TrackColor(0.5f, 0.5f, 0.5f)]
    [TrackClipType(typeof(PhysicsKinematicOverrideClip))]
    [TrackBindingType(typeof(GameObject))]
    [DisplayName("BovineLabs/Physics/Kinematic Override")]
    public sealed class PhysicsKinematicOverrideTrack : DOTSTrack
    {
    }
}