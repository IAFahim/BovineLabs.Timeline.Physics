using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics.Chains
{
    public struct ChainLink : IComponentData
    {
        public int Index;
        public Entity Root;
        public Entity AnimationBone;
    }

    public struct ChainRoot : IComponentData
    {
        public int LinkCount;
    }

    public struct ChainFollowData
    {
        public float PositionStrength;
        public float OrientationStrength;
        public float PositionHalflife;
        public float OrientationHalflife;
        public float MaxLinearSpeed;
        public float MaxAngularSpeed;
    }

    public struct ChainFollowAnimated : IAnimatedComponent<ChainFollowData>, IPreparable
    {
        public ChainFollowData AuthoredData;
        [CreateProperty] public ChainFollowData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveChainFollow : IActive<ChainFollowData>
    {
        public ChainFollowData Value;
        public ChainFollowData Config
        {
            get => Value;
            set => Value = value;
        }
    }

    public readonly struct ChainFollowMixer : IMixer<ChainFollowData>
    {
        public ChainFollowData Lerp(in ChainFollowData a, in ChainFollowData b, in float s)
        {
            return new ChainFollowData
            {
                PositionStrength = math.lerp(a.PositionStrength, b.PositionStrength, s),
                OrientationStrength = math.lerp(a.OrientationStrength, b.OrientationStrength, s),
                PositionHalflife = math.lerp(a.PositionHalflife, b.PositionHalflife, s),
                OrientationHalflife = math.lerp(a.OrientationHalflife, b.OrientationHalflife, s),
                MaxLinearSpeed = math.lerp(a.MaxLinearSpeed, b.MaxLinearSpeed, s),
                MaxAngularSpeed = math.lerp(a.MaxAngularSpeed, b.MaxAngularSpeed, s)
            };
        }

        public ChainFollowData Add(in ChainFollowData a, in ChainFollowData b)
        {
            return new ChainFollowData
            {
                PositionStrength = math.saturate(a.PositionStrength + b.PositionStrength),
                OrientationStrength = math.saturate(a.OrientationStrength + b.OrientationStrength),
                PositionHalflife = a.PositionHalflife,
                OrientationHalflife = a.OrientationHalflife,
                MaxLinearSpeed = a.MaxLinearSpeed,
                MaxAngularSpeed = a.MaxAngularSpeed
            };
        }
    }
}
