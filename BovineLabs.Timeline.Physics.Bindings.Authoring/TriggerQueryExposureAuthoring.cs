namespace BovineLabs.Timeline.Physics.Bindings.Authoring
{
    using BovineLabs.Timeline.Physics.Bindings;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Put on a light-source object to make it an illumination source for <see cref="TriggerQueryExposureSystem"/>.
    /// The source lights nearby <see cref="TriggerQueryExposedAuthoring"/> bodies with a linear distance falloff.
    /// </summary>
    [DisallowMultipleComponent]
    public class TriggerExposureSourceAuthoring : MonoBehaviour
    {
        [Tooltip("Peak illumination contributed at the source position (falls off linearly to 0 at Range).")]
        public float Intensity = 1f;

        [Tooltip("Distance (m) at which this source contributes nothing.")]
        public float Range = 10f;

        private class Baker : Baker<TriggerExposureSourceAuthoring>
        {
            public override void Bake(TriggerExposureSourceAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Renderable);
                this.AddComponent(entity, new TriggerExposureSource
                {
                    Intensity = authoring.Intensity,
                    Range = authoring.Range,
                });
            }
        }
    }

    /// <summary>
    /// Put on a candidate body to give it the <see cref="TriggerQueryExposure"/> the LightExposureGate reads.
    /// <see cref="TriggerQueryExposureSystem"/> writes its <c>Value</c> each fixed step from the authored sources.
    /// </summary>
    [DisallowMultipleComponent]
    public class TriggerQueryExposedAuthoring : MonoBehaviour
    {
        private class Baker : Baker<TriggerQueryExposedAuthoring>
        {
            public override void Bake(TriggerQueryExposedAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent<TriggerQueryExposure>(entity);
            }
        }
    }
}
