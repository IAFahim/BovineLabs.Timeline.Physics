namespace BovineLabs.Timeline.Physics.TriggerEvents
{

    using BovineLabs.Core.Iterators;
    using BovineLabs.Reaction.Data.Core;
    using EntityLinks;
    using BovineLabs.Timeline.EntityLinks.Data;
    using Unity.Entities;

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
            if (filter.IgnoreTarget != Target.None)
            {
                var ignoredEntity = targets.Get(filter.IgnoreTarget, self);
                if (ignoredEntity != Entity.Null)
                {
                    EntityLinkResolver.TryResolveRoot(ignoredEntity, sources, out var ignoredRoot);
                    EntityLinkResolver.TryResolveRoot(other, sources, out var otherRoot);
                    
                    if (ignoredRoot == otherRoot)
                        return false;
                }
            }

            if (filter.LinkFilterBlob.IsCreated)
            {
                bool hasValidLink = false;

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
                    return false;
            }

            return true;
        }
    }
}