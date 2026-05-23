using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
{
    public static class PhysicsTriggerFiltering
    {
        public static bool IsValidTarget(
            Entity self,
            Entity other,
            in PhysicsTriggerFilterData filter,
            in Targets targets,
            in UnsafeComponentLookup<EntityLinkSource> sources,
            in UnsafeBufferLookup<EntityLinkEntry> links)
        {
            // 1. Check "Ignore Target"
            if (filter.IgnoreTarget != Target.None)
            {
                var ignoredEntity = targets.Get(filter.IgnoreTarget, self);
                if (ignoredEntity != Entity.Null)
                {
                    // Compare Roots to ensure we don't hit ANY collider attached to the ignored entity
                    EntityLinkResolver.TryResolveRoot(ignoredEntity, sources, out var ignoredRoot);
                    EntityLinkResolver.TryResolveRoot(other, sources, out var otherRoot);
                    
                    if (ignoredRoot == otherRoot)
                        return false; // Ignore! They belong to the same root hierarchy.
                }
            }

            // 2. Check "Link ID Filter Blob"
            if (filter.LinkFilterBlob.IsCreated && filter.LinkFilterBlob.Value.ValidLinkKeys.Length > 0)
            {
                bool hasValidLink = false;
                
                // We need to reverse-lookup 'other' to see if it matches any of the allowed keys
                if (EntityLinkResolver.TryResolveRoot(other, sources, out var otherRoot) &&
                    links.TryGetBuffer(otherRoot, out var otherLinks))
                {
                    for (int i = 0; i < otherLinks.Length; i++)
                    {
                        if (otherLinks[i].Target == other)
                        {
                            var key = otherLinks[i].Key;
                            ref var validKeys = ref filter.LinkFilterBlob.Value.ValidLinkKeys;
                            
                            for (int j = 0; j < validKeys.Length; j++)
                            {
                                if (validKeys[j] == key)
                                {
                                    hasValidLink = true;
                                    break;
                                }
                            }
                        }
                        if (hasValidLink) break;
                    }
                }

                if (!hasValidLink)
                    return false; // Failed the filter
            }

            return true;
        }
    }
}