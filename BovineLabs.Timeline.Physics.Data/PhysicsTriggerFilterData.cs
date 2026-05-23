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
        // Target to ignore (e.g., Target.Owner to ignore anything sharing the owner's root)
        public Target IgnoreTarget;
        
        // Optional blob of allowed link keys
        public BlobAssetReference<PhysicsTriggerLinkBlob> LinkFilterBlob;
    }
}