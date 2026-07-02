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

        /// <summary> Bake an ascending list of SQUARED distance thresholds for DistanceBand. Input is in metres. </summary>
        public static BlobAssetReference<PhysicsTriggerDistanceBandBlob> BakeDistanceBandBlob(IBaker baker,
            float[] distanceThresholds)
        {
            if (distanceThresholds == null || distanceThresholds.Length == 0)
                return default;

            // Sort ascending and square (sqrt-free bucketing at runtime).
            var sorted = (float[])distanceThresholds.Clone();
            System.Array.Sort(sorted);

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerDistanceBandBlob>();
            var array = builder.Allocate(ref root.Thresholds, sorted.Length);
            for (var i = 0; i < sorted.Length; i++)
            {
                var d = sorted[i];
                array[i] = d * d;
            }

            var blob = builder.CreateBlobAssetReference<PhysicsTriggerDistanceBandBlob>(Allocator.Persistent);
            baker.AddBlobAsset(ref blob, out _);
            builder.Dispose();

            return blob;
        }

        /// <summary> Bake an ascending magnitude band table for ScaledMagnitude. Thresholds are NOT squared. </summary>
        public static BlobAssetReference<PhysicsTriggerDistanceBandBlob> BakeMagnitudeBandBlob(IBaker baker,
            float[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0)
                return default;

            var sorted = (float[])thresholds.Clone();
            System.Array.Sort(sorted);

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerDistanceBandBlob>();
            var array = builder.Allocate(ref root.Thresholds, sorted.Length);
            for (var i = 0; i < sorted.Length; i++)
                array[i] = sorted[i];

            var blob = builder.CreateBlobAssetReference<PhysicsTriggerDistanceBandBlob>(Allocator.Persistent);
            baker.AddBlobAsset(ref blob, out _);
            builder.Dispose();

            return blob;
        }

        /// <summary> Bake the CategoryPriority / CategoryOrdinal tier list (parallel mask → ordinal arrays). </summary>
        public static BlobAssetReference<PhysicsTriggerCategoryTierBlob> BakeCategoryTierBlob(IBaker baker,
            uint[] masks, int[] ordinals)
        {
            if (masks == null || masks.Length == 0)
                return default;

            var count = ordinals != null ? System.Math.Min(masks.Length, ordinals.Length) : masks.Length;
            if (count == 0)
                return default;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PhysicsTriggerCategoryTierBlob>();
            var maskArray = builder.Allocate(ref root.Masks, count);
            var ordArray = builder.Allocate(ref root.Ordinals, count);
            for (var i = 0; i < count; i++)
            {
                maskArray[i] = masks[i];
                ordArray[i] = ordinals != null ? ordinals[i] : i;
            }

            var blob = builder.CreateBlobAssetReference<PhysicsTriggerCategoryTierBlob>(Allocator.Persistent);
            baker.AddBlobAsset(ref blob, out _);
            builder.Dispose();

            return blob;
        }
    }
}