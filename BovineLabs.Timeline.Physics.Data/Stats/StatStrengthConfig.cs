using BovineLabs.Essence.Data;
using BovineLabs.Reaction.Data.Core;

namespace BovineLabs.Timeline.Physics
{
    public struct StatStrengthConfig
    {
        public StatKey Stat;
        public Target ReadFrom;
        public ushort LinkKey;

        public bool IsEnabled() => Stat.Value != 0;
    }
}
