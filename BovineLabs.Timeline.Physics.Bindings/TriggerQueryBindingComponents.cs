namespace BovineLabs.Timeline.Physics.Bindings
{
    using Unity.Entities;

    /// <summary>
    /// Marks a trigger-volume body as a "zone" for <see cref="TriggerQueryZoneVolumeSystem"/>. A candidate body that
    /// carries the enableable <see cref="TriggerQueryZoneTag"/> gets it enabled while it currently overlaps any body
    /// carrying this marker (via the body's <c>StatefulTriggerEvent</c> buffer), and disabled once it leaves them all.
    /// This is one reference driver for the ZoneStateGate mechanism; a game may replace it with any other condition.
    /// </summary>
    public struct TriggerQueryZoneVolume : IComponentData
    {
    }

    /// <summary>
    /// An authored point light source read by <see cref="TriggerQueryExposureSystem"/> to drive
    /// <see cref="TriggerQueryExposure.Value"/> on nearby bodies. Illumination falls off linearly with distance:
    /// contribution = <see cref="Intensity"/> * saturate(1 - distance / <see cref="Range"/>). This is a minimal,
    /// deterministic, occlusion-free light model provided so the LightExposureGate has a source of truth out of the
    /// box; a game with a real light/illumination system should write <see cref="TriggerQueryExposure.Value"/> from
    /// that instead and drop this driver.
    /// </summary>
    public struct TriggerExposureSource : IComponentData
    {
        public float Intensity;
        public float Range;
    }

    /// <summary>
    /// A gameplay request to taunt: while enabled, <see cref="TriggerQueryTauntSystem"/> stamps
    /// <see cref="TriggerQueryTaunt.UntilTime"/> to the current fixed-step world time plus <see cref="Duration"/>,
    /// then disables the request. Gameplay (AI, a reaction action, a debug console, a test) enables it with a
    /// duration to make this body an instant, locked TauntOverride winner for that window.
    /// </summary>
    public struct TriggerQueryTauntRequest : IComponentData, IEnableableComponent
    {
        public float Duration;
    }
}
