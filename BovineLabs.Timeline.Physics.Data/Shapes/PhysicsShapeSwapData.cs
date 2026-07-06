using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Physics;
using Unity.Properties;
using Collider = Unity.Physics.Collider;

namespace BovineLabs.Timeline.Physics
{
    /// <summary> While-active SWAP of the bound body's whole collider blob to a different baked shape. </summary>
    public struct PhysicsShapeSwapData : IComponentData
    {
        /// <summary> The replacement collider baked from the clip's authored shape (carries its own filter). </summary>
        public BlobAssetReference<Collider> NewCollider;

        /// <summary> Restore the original collider when the clip ends. </summary>
        public bool RestoreOnExit;
    }

    public struct PhysicsShapeSwapAnimated : IAnimatedComponent<PhysicsShapeSwapData>, IPreparable
    {
        public PhysicsShapeSwapData AuthoredData;
        [CreateProperty] public PhysicsShapeSwapData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveShapeSwap : IActive<PhysicsShapeSwapData>
    {
        public PhysicsShapeSwapData Config { get; set; }
    }

    public struct PhysicsShapeSwapState : IComponentData, IRestorableState
    {
        public bool Fired;

        /// <summary> The body's original collider blob, restored on exit. Swap is share-safe (it re-points the
        /// PhysicsCollider reference, never mutates the blob), so no Force Unique is required. </summary>
        public BlobAssetReference<Collider> Original;

        public bool RestorePending => this.Fired;
    }
}
