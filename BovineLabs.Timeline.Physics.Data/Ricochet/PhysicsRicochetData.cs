using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.EntityLinks.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics
{
    public struct PhysicsRicochetData : IComponentData
    {
        public int MaxBounces;
        public float MaxDistance;
        public float MinGrazingAngle;
        public uint RicochetMask;
        public uint TerminalHitMask;

        public ushort HitConditionKey;
        public EntityLinkRef HitRouteTo;

        public EntityLinkRef RayOrigin;
        public EntityLinkRef RayDirection;

        public StatSource Strength;
    }

    public struct PhysicsRicochetAnimated : IAnimatedComponent<PhysicsRicochetData>, IPreparable
    {
        public PhysicsRicochetData AuthoredData;
        [CreateProperty] public PhysicsRicochetData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveRicochet : IActive<PhysicsRicochetData>
    {
        public PhysicsRicochetData Config { get; set; }
    }

    public struct PhysicsRicochetState : IComponentData
    {
        public bool Fired;
    }
}