using BovineLabs.Timeline.EntityLinks.Authoring;
using Unity.Collections;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public static class PhysicsTriggerBakingUtility
    {
        public static BlobAssetReference<PhysicsTriggerLinkBlob> BakeFilterBlob(IBaker baker, EntityLinkSchema[] requireLinks)
        {
            if (requireLinks == null || requireLinks.Length == 0)
                return default;

            int validCount = 0;
            for (int i = 0; i < requireLinks.Length; i++)
            {
                if (EntityLinkAuthoringUtility.TryGetKey(requireLinks[i], out _))
                    validCount++;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerLinkBlob>();
            var array = builder.Allocate(ref root.ValidLinkKeys, validCount);

            int index = 0;
            for (int i = 0; i < requireLinks.Length; i++)
            {
                if (EntityLinkAuthoringUtility.TryGetKey(requireLinks[i], out var reqKey))
                    array[index++] = reqKey;
            }

            var filterBlob = builder.CreateBlobAssetReference<PhysicsTriggerLinkBlob>(Allocator.Persistent);
            baker.AddBlobAsset(ref filterBlob, out _);
            builder.Dispose();

            return filterBlob;
        }
    }
}
