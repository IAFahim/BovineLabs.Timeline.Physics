using System.Runtime.CompilerServices;
using BovineLabs.Core.Iterators;
using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.EntityLinks;
using BovineLabs.Timeline.EntityLinks.Data;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics
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

            if (!EntityLinkResolver.TryResolve(self, targets, config.ReadFrom, config.LinkKey,
                    linkSources, linkEntries, out var statEntity))
                return 1f;

            if (!statLookup.TryGetBuffer(statEntity, out var stats))
                return 1f;

            return stats.AsMap().GetValueFloat(config.Stat);
        }
    }
}