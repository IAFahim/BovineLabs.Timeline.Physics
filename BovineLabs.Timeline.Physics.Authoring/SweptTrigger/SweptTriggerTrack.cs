namespace BovineLabs.Timeline.Physics.Authoring
{
    using System;
    using System.ComponentModel;
    using BovineLabs.Timeline.Authoring;
    using UnityEngine.Timeline;

    /// <summary>
    /// The SWEPT companion to <see cref="StatefulTriggerTrack"/>. Hosts the EXACT same clips, so designers
    /// author identically — but it binds to a <see cref="SweptTriggerSourceAuthoring"/> whose dummy collider
    /// is swept against the world each active frame instead of relying on the physics simulation's discrete
    /// trigger events. Use this for animation-driven weapons (a swinging blade) where a static or
    /// teleporting collider would tunnel or sit at the wrong place; use <see cref="StatefulTriggerTrack"/>
    /// for static volumes / overlap zones. Both feed the same StatefulTriggerEvent buffer and the same
    /// downstream clip systems.
    /// </summary>
    [Serializable]
    [TrackClipType(typeof(PhysicsTriggerInstantiateClip))]
    [TrackClipType(typeof(PhysicsTriggerConditionClip))]
    [TrackClipType(typeof(PhysicsTriggerForceClip))]
    [TrackClipType(typeof(PhysicsBreakForceClip))]
    [TrackClipType(typeof(PhysicsTriggerQueryClip))]
    [TrackColor(0.95f, 0.45f, 0.1f)]
    [DisplayName("BovineLabs/Physics/Swept Trigger")]
    [TrackBindingType(typeof(SweptTriggerSourceAuthoring))]
    public class SweptTriggerTrack : DOTSTrack
    {
    }
}
