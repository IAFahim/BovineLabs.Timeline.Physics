namespace BovineLabs.Timeline.Physics
{
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;

    /// <summary>
    /// Marks an entity as a SWEPT trigger source — the companion to the simulation-driven stateful trigger.
    /// Each frame a clip on a <c>SweptTriggerTrack</c> is active, <c>SweptTriggerSystem</c> sweeps
    /// <see cref="Collider"/> from the entity's PREVIOUS to its CURRENT world transform and writes the
    /// resulting Enter/Stay/Exit edges into the entity's <c>StatefulTriggerEvent</c> buffer — the exact
    /// buffer the physics simulation fills for a real trigger. Every existing Stateful Trigger clip
    /// (Instantiate / Condition / Force / BreakForce / Query) therefore works unchanged, but the detection
    /// is swept so a fast melee swing can never tunnel past a thin target.
    /// </summary>
    public struct SweptTriggerConfig : IComponentData
    {
        /// <summary>The "dummy" collider blob swept against the world (a capsule baked from authoring params).</summary>
        public BlobAssetReference<Collider> Collider;

        /// <summary>
        /// Sub-steps interpolating prev-&gt;cur transform per frame (clamped to &gt;= 1). Higher values give
        /// better coverage for very fast swings (rotation is interpolated, position is the cast segment).
        /// </summary>
        public int SubSteps;

        // Local-space capsule geometry mirror of <see cref="Collider"/>, kept so debug drawers (and any
        // gameplay introspection) can render/reason about the swept volume without dereferencing the blob.
        public float3 Vertex0;
        public float3 Vertex1;
        public float Radius;
    }
}
