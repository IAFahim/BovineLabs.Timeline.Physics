using System;

namespace BovineLabs.Timeline.Physics
{
    [Flags]
    public enum VelocityResetFlags : byte
    {
        None = 0,
        Linear = 1 << 0,
        Angular = 1 << 1,
        Both = Linear | Angular
    }
}