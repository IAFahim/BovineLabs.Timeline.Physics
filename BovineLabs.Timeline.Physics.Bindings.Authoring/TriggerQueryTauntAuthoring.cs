namespace BovineLabs.Timeline.Physics.Bindings.Authoring
{
    using BovineLabs.Timeline.Physics.Bindings;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Put on a candidate body to give it the <see cref="TriggerQueryTaunt"/> the TauntOverride selection reads plus
    /// the enableable <see cref="TriggerQueryTauntRequest"/> gameplay flips to trigger a taunt. The request is baked
    /// disabled; enabling it (from a reaction action, AI, or test) makes <see cref="TriggerQueryTauntSystem"/> stamp
    /// an expiry <see cref="Duration"/> seconds into the future on the correct fixed-step clock.
    /// </summary>
    [DisallowMultipleComponent]
    public class TriggerQueryTauntAuthoring : MonoBehaviour
    {
        [Tooltip("Seconds the taunt stays active once requested. Gameplay enables the request; the system stamps UntilTime = now + Duration.")]
        public float Duration = 2f;

        private class Baker : Baker<TriggerQueryTauntAuthoring>
        {
            public override void Bake(TriggerQueryTauntAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent<TriggerQueryTaunt>(entity);
                this.AddComponent(entity, new TriggerQueryTauntRequest { Duration = authoring.Duration });
                this.SetComponentEnabled<TriggerQueryTauntRequest>(entity, false);
            }
        }
    }
}
