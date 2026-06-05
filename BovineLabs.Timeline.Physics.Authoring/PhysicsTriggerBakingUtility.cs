using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public static class PhysicsTriggerBakingUtility
    {
        public static BlobAssetReference<PhysicsTriggerLinkBlob> BakeFilterBlob(IBaker baker,
            EntityLinkSchema[] requireLinks)
        {
            if (requireLinks == null || requireLinks.Length == 0)
                return default;

            var validCount = 0;
            for (var i = 0; i < requireLinks.Length; i++)
                if (requireLinks[i] != null && EntityLinkAuthoringUtility.TryGetKey(requireLinks[i], out _))
                    validCount++;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerLinkBlob>();
            var array = builder.Allocate(ref root.ValidLinkKeys, validCount);

            var index = 0;
            for (var i = 0; i < requireLinks.Length; i++)
                if (requireLinks[i] != null && EntityLinkAuthoringUtility.TryGetKey(requireLinks[i], out var reqKey))
                    array[index++] = reqKey;

            var filterBlob = builder.CreateBlobAssetReference<PhysicsTriggerLinkBlob>(Allocator.Persistent);
            baker.AddBlobAsset(ref filterBlob, out _);
            builder.Dispose();

            return filterBlob;
        }
    }
}