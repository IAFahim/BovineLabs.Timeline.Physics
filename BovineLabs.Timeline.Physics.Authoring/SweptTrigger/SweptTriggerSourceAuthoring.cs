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
    /// simulation-driven stateful trigger uses — greatly reducing tunnelling on fast melee swings, with no
    /// changes to any existing trigger clip or system.
    /// <para>
    /// Attach to the bone-anchored weapon object (its animated world transform is the sweep path). The
    /// capsule is authored as parameters here rather than a <c>PhysicsShapeAuthoring</c> so it never gets
    /// merged into the character's compound collider. To ignore the wielder, also add a
    /// <c>TargetsAuthoring</c> with Owner = the character and leave the clip's Ignore Target = Owner.
    /// The swept volume is authored in METRES and ignores the object's transform scale (the query runs at
    /// QueryColliderScale = 1) — size it via radius/length on an unscaled object; the Scene gizmo shows the
    /// true unscaled volume.
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

        [Tooltip("Local axis the capsule extends along — match your blade's direction (Y=up, Z=forward, X=side).")]
        public Axis axis = Axis.Y;

        [Tooltip("Physics categories the sweep can hit. EMPTY = hits NOTHING (silent fail). Defaults to Everything; " +
                 "narrow it to your enemy/target category for clean single-target melee.")]
        public PhysicsCategoryTags collidesWith = new PhysicsCategoryTags { Value = ~0u };

        [Tooltip("Sub-steps per frame interpolating the prev->cur transform (>= 1). Raise for very fast swings; " +
                 "1 is fine for most attacks.")]
        [Min(1)]
        public int subSteps = 1;

#if UNITY_EDITOR
        // Draws the swept capsule volume in the Scene view at AUTHOR time (not just play), so designers can size/place it.
        // The empty-CollidesWith and missing-Owner traps are surfaced (with one-click fixes) by the custom inspector,
        // not via OnValidate — OnValidate re-logged on every scene/prefab load and edit, spamming the console.
        private void OnDrawGizmosSelected()
        {
            var half = Mathf.Max(0f, this.length * 0.5f);
            var r = Mathf.Max(0.001f, this.radius);
            var dir = this.axis switch
            {
                Axis.X => Vector3.right,
                Axis.Z => Vector3.forward,
                _ => Vector3.up,
            };

            var v0 = this.offset - (dir * half);
            var v1 = this.offset + (dir * half);
            var prev = Gizmos.matrix;

            // Draw in UNSCALED world space (position + rotation only). The runtime sweep queries the capsule via
            // RigidTransform with QueryColliderScale = 1, so the actual hit volume ignores the object's lossyScale.
            // Drawing through localToWorldMatrix would squash the gizmo by a non-unit (e.g. 0.18,0.5,0.18) scale and
            // misrepresent what gets hit — keep the gizmo honest by matching the query's unscaled metres.
            Gizmos.matrix = Matrix4x4.TRS(this.transform.position, this.transform.rotation, Vector3.one);
            Gizmos.color = this.collidesWith.Value == 0 ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.4f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(v0, r);
            Gizmos.DrawWireSphere(v1, r);
            var p1 = this.axis == Axis.Y ? Vector3.right : Vector3.up;
            var p2 = Vector3.Cross(dir, p1);
            Gizmos.DrawLine(v0 + (p1 * r), v1 + (p1 * r));
            Gizmos.DrawLine(v0 - (p1 * r), v1 - (p1 * r));
            Gizmos.DrawLine(v0 + (p2 * r), v1 + (p2 * r));
            Gizmos.DrawLine(v0 - (p2 * r), v1 - (p2 * r));
            Gizmos.matrix = prev;
        }
#endif

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
                    Vertex0 = geom.Vertex0,
                    Vertex1 = geom.Vertex1,
                    Radius = r,
                });
                this.AddComponent(entity, default(SweptTriggerState));
                this.AddBuffer<SweptTriggerHit>(entity);

                // The shared seam: the same buffer the simulation-driven stateful trigger fills.
                this.AddBuffer<StatefulTriggerEvent>(entity);
            }
        }
    }
}
