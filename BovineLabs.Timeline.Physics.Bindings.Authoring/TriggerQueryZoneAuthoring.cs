namespace BovineLabs.Timeline.Physics.Bindings.Authoring
{
    using BovineLabs.Timeline.Physics.Bindings;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Put on a trigger-volume body (alongside its trigger <c>PhysicsShapeAuthoring</c> / raise-trigger-events setup)
    /// to mark it as a zone that <see cref="TriggerQueryZoneVolumeSystem"/> counts toward a candidate's
    /// <see cref="TriggerQueryZoneTag"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class TriggerQueryZoneVolumeAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TriggerQueryZoneVolumeAuthoring>
        {
            public override void Bake(TriggerQueryZoneVolumeAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent<TriggerQueryZoneVolume>(entity);
            }
        }
    }

    /// <summary>
    /// Put on a candidate body (alongside a <c>StatefulTriggerEventAuthoring</c> so it collects overlap events) to
    /// give it the enableable <see cref="TriggerQueryZoneTag"/> the ZoneStateGate reads. The tag is baked disabled;
    /// <see cref="TriggerQueryZoneVolumeSystem"/> enables it while the body sits inside a
    /// <see cref="TriggerQueryZoneVolume"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class TriggerQueryZoneMemberAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TriggerQueryZoneMemberAuthoring>
        {
            public override void Bake(TriggerQueryZoneMemberAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent<TriggerQueryZoneTag>(entity);
                this.SetComponentEnabled<TriggerQueryZoneTag>(entity, false);
            }
        }
    }
}
