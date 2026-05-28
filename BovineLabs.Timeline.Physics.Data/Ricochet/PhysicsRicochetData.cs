using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
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
        public Target HitRouteTo;
        public ushort HitRouteLinkKey;

        public Target RayOrigin;
        public ushort RayOriginLinkKey;
        public Target RayDirection;
        public ushort RayDirectionLinkKey;

        public StatStrengthConfig Strength;
    }

    public struct PhysicsRicochetAnimated : IAnimatedComponent<PhysicsRicochetData>
    {
        public PhysicsRicochetData AuthoredData;
        [CreateProperty] public PhysicsRicochetData Value { get; set; }
        public PhysicsRicochetData GetAuthoredData() => AuthoredData;
    }

    public struct ActiveRicochet : IComponentData, IEnableableComponent
    {
        public PhysicsRicochetData Config;
    }

    public struct PhysicsRicochetState : IComponentData
    {
        public bool Fired;
    }

    public struct PhysicsRicochetMixer : IMixer<PhysicsRicochetData>
    {
        public PhysicsRicochetData Lerp(in PhysicsRicochetData a, in PhysicsRicochetData b, in float s)
        {
            return s >= 0.5f ? b : a;
        }

        public PhysicsRicochetData Add(in PhysicsRicochetData a, in PhysicsRicochetData b)
        {
            return b; // Left-hand side or right-hand side, since ricochet doesn't blend meaningfully
        }
    }
}
