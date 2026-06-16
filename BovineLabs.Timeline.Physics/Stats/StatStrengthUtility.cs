using System.Runtime.CompilerServices;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Stats
{
    public static class StatStrengthUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Resolve(
            in StatStrengthConfig config,
            Entity self,
            in Targets targets,
            in UnsafeComponentLookup<EntityLinkSource> linkSources,
            in UnsafeBufferLookup<EntityLinkEntry> linkEntries,
            in BufferLookup<Stat> statLookup)
        {
            if (!config.IsEnabled())
                return 1f;

            var statEntity = config.LinkKey == 0
                ? targets.Get(config.ReadFrom, self)
                : EntityLinkResolver.TryResolve(self, targets, config.ReadFrom, config.LinkKey,
                    linkSources, linkEntries, out var linked)
                    ? linked
                    : Entity.Null;

            if (statEntity == Entity.Null)
                return 1f;

            if (!statLookup.TryGetBuffer(statEntity, out var stats))
                return 1f;

            return stats.AsMap().GetValueFloat(config.Stat, 1f);
        }
    }
}