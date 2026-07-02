using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    /// <summary> While-active per-axis resize of the bound body's PHYSICS collider only (not its renderer). </summary>
    public struct PhysicsShapeResizeData : IComponentData
    {
        /// <summary> Per-axis multiplier on the collider geometry (uniform = all equal). Radius-based shapes
        /// (sphere/capsule/cylinder) take their radius from X. </summary>
        public float3 Scale;

        /// <summary> Restore the original collider size when the clip ends. </summary>
        public bool RestoreOnExit;
    }

    public struct PhysicsShapeResizeAnimated : IAnimatedComponent<PhysicsShapeResizeData>, IPreparable
    {
        public PhysicsShapeResizeData AuthoredData;
        [CreateProperty] public PhysicsShapeResizeData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveShapeResize : IActive<PhysicsShapeResizeData>
    {
        public PhysicsShapeResizeData Config { get; set; }
    }

    /// <summary>
    /// Captured ORIGINAL primitive geometry so the apply is idempotent (sets absolute = original*scale each frame,
    /// never compounding) and exactly restorable. Packed to cover sphere/box/capsule/cylinder without a union:
    /// the meaning of each field depends on <see cref="Type"/>.
    /// </summary>
    public struct PhysicsShapeResizeState : IComponentData
    {
        public bool Fired;

        /// <summary> Set once the "shared collider, resize skipped" diagnostic has fired, so it warns once per body
        /// instead of every active frame. Kept separate from Fired so the restore branch never runs for a skip. </summary>
        public bool WarnedShared;

        /// <summary> (byte)Unity.Physics.ColliderType captured on enter; 255 = unsupported (convex/mesh/compound). </summary>
        public byte Type;

        /// <summary> sphere/box/cylinder center; capsule Vertex0. </summary>
        public float3 OrigCenter;

        /// <summary> box Size; capsule Vertex1; cylinder (Height, BevelRadius, SideCount). </summary>
        public float3 OrigB;

        /// <summary> sphere/capsule/cylinder Radius; box BevelRadius. </summary>
        public float OrigRadius;

        /// <summary> box/cylinder Orientation. </summary>
        public quaternion OrigOrient;
    }
}
