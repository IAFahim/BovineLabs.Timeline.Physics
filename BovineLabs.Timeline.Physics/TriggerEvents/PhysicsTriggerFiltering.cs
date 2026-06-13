using BovineLabs.Core.Iterators;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.TriggerEvents
{
    public static class PhysicsTriggerFiltering
    {
        public static Entity ResolveRoot(Entity entity, in UnsafeComponentLookup<EntityLinkSource> sources)
        {
            EntityLinkResolver.TryResolveRoot(entity, sources, out var root);
            return root;
        }

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
                var hasValidLink = false;

                if (EntityLinkResolver.TryResolveRoot(other, sources, out var otherRoot) &&
                    links.TryGetBuffer(otherRoot, out var otherLinks))
                    for (var i = 0; i < otherLinks.Length; i++)
                    {
                        if (otherLinks[i].Target == other)
                        {
                            var key = otherLinks[i].Key;
                            ref var validKeys = ref filter.LinkFilterBlob.Value.ValidLinkKeys;

                            for (var j = 0; j < validKeys.Length; j++)
                                if (validKeys[j] == key)
                                {
                                    hasValidLink = true;
                                    break;
                                }
                        }

                        if (hasValidLink) break;
                    }

                if (!hasValidLink)
                    return false;
            }

            return true;
        }
    }
}