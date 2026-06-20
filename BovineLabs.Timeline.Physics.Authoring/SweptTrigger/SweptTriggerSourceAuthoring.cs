namespace BovineLabs.Timeline.Physics.Authoring
{
    using BovineLabs.Core.PhysicsStates;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Physics;
    using Unity.Physics.Authoring;
    using UnityEngine;

    /// <summary>
    /// Binding target for <see cref="SweptTriggerTrack"/>. Defines a "dummy" capsule collider that is SWEPT
    /// against the world each active frame to drive the same <c>StatefulTriggerEvent</c> buffer the
    /// simulation-driven stateful trigger uses — catching fast melee swings without tunneling, with no
    /// changes to any existing trigger clip or system.
    /// <para>
    /// Attach to the bone-anchored weapon object (its animated world transform is the sweep path). The
    /// capsule is authored as parameters here rather than a <c>PhysicsShapeAuthoring</c> so it never gets
    /// merged into the character's compound collider. To ignore the wielder, also add a
    /// <c>TargetsAuthoring</c> with Owner = the character and leave the clip's Ignore Target = Owner.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class SweptTriggerSourceAuthoring : MonoBehaviour
    {
        public enum Axis
        {
            X,
            Y,
            Z,
        }

        [Tooltip("Capsule radius of the swept blade volume (metres).")]
        [Min(0.001f)]
        public float radius = 0.06f;

        [Tooltip("Capsule length along the chosen local axis (metres) — span it down the blade.")]
        [Min(0f)]
        public float length = 0.8f;

        [Tooltip("Local-space centre offset of the capsule from this object's origin.")]
        public Vector3 offset = Vector3.zero;

        [Tooltip("Local axis the capsule extends along.")]
        public Axis axis = Axis.Y;

        [Tooltip("Which physics categories the sweep is allowed to hit.")]
        public PhysicsCategoryTags collidesWith;

        [Tooltip("Sub-steps per frame interpolating the prev->cur transform (>= 1). Raise for very fast swings.")]
        [Min(1)]
        public int subSteps = 1;

        private class Baker : Baker<SweptTriggerSourceAuthoring>
        {
            public override void Bake(SweptTriggerSourceAuthoring authoring)
            {
                // Dynamic so the entity carries LocalTransform + LocalToWorld; anchored under the bone its
                // LocalToWorld is the animated weapon transform the sweep follows.
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);

                var half = math.max(0f, authoring.length * 0.5f);
                var r = math.max(0.001f, authoring.radius);
                var dir = authoring.axis switch
                {
                    Axis.X => new float3(1f, 0f, 0f),
                    Axis.Z => new float3(0f, 0f, 1f),
                    _ => new float3(0f, 1f, 0f),
                };

                float3 centre = authoring.offset;
                var geom = new CapsuleGeometry
                {
                    Vertex0 = centre - (dir * half),
                    Vertex1 = centre + (dir * half),
                    Radius = r,
                };

                var filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    CollidesWith = authoring.collidesWith.Value,
                    GroupIndex = 0,
                };

                var blob = Unity.Physics.CapsuleCollider.Create(geom, filter);
                this.AddBlobAsset(ref blob, out _);

                this.AddComponent(entity, new SweptTriggerConfig
                {
                    Collider = blob,
                    SubSteps = math.max(1, authoring.subSteps),
                });
                this.AddComponent(entity, default(SweptTriggerState));
                this.AddBuffer<SweptTriggerHit>(entity);

                // The shared seam: the same buffer the simulation-driven stateful trigger fills.
                this.AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }
}
