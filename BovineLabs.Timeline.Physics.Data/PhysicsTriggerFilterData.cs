using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsTriggerLinkBlob
    {
        public BlobArray<ushort> ValidLinkKeys;
    }

    public struct PhysicsTriggerFilterData : IComponentData
    {
        public Target IgnoreTarget;

        public BlobAssetReference<PhysicsTriggerLinkBlob> LinkFilterBlob;

        public PhysicsTriggerHitMode HitMode;
    }
}