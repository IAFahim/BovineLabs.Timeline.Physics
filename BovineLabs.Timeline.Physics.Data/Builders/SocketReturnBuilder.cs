using BovineLabs.Core.EntityCommands;

namespace BovineLabs.Timeline.Physics.Sockets
{
    public struct SocketReturnBuilder
    {
        public SocketReturnData AuthoredData;

        public void ApplyTo<T>(ref T builder)
            where T : struct, IEntityCommands
        {
            builder.AddComponent(new SocketReturnAnimated { AuthoredData = AuthoredData });
        }
    }
}