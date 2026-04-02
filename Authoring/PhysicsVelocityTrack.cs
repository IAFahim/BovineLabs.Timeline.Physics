using UnityEngine;
using BovineLabs.Timeline.Authoring;
using System;
using System.ComponentModel;
using Unity.Physics.Authoring;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    [Serializable]
    [TrackClipType(typeof(PhysicsVelocityClip))]
    [TrackColor(0.9f, 0.4f, 0.1f)]
    [TrackBindingType(typeof(PhysicsBodyAuthoring))]
    [DisplayName("BovineLabs/Timeline/Physics/Velocity")]
    public class PhysicsVelocityTrack : DOTSTrack
    {
        protected override void Bake(BakingContext context)
        {
        }
    }
}
