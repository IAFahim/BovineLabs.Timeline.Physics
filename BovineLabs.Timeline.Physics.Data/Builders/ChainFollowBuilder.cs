using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Chains
{
    public struct ChainFollowBuilder
    {
        public ChainFollowData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new ChainFollowAnimated { AuthoredData = AuthoredData });
        }
    }
}