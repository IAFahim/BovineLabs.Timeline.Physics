using BovineLabs.Reaction.Data.Core;
using Unity.Entities;

namespace BovineLabs.Timeline.Physics.Chains
{
    public enum ChainGrabMode : byte
    {
        Stick,
        Wrap,
        Reel
    }

    public struct ChainGrabConfig : IComponentData
    {
        public ChainGrabMode Mode;
        public uint HitMask;
        public bool EnableCollision;
        public Target ReelAnchor;
        public float ReelSpeed;
        public float ReelMinDistance;
    }

    public struct ChainGrabArmed : IComponentData, IEnableableComponent
    {
    }

    public struct ChainLinkGrabbed : IComponentData, IEnableableComponent
    {
    }

    [InternalBufferCapacity(8)]
    public struct ChainAnchor : IBufferElementData
    {
        public Entity Joint;
        public Entity Link;
    }

    public struct ChainReel : IComponentData
    {
        public float Speed;
        public float MinDistance;
    }

    public struct ChainReleaseRequest : IComponentData, IEnableableComponent
    {
    }
}