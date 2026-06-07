using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace BovineLabs.Timeline.Physics.Sockets
{
    public struct SocketReturnData
    {
        public Target Socket;
        public float3 LocalPosition;
        public quaternion LocalRotation;
        public float PositionHalflife;
        public float RotationHalflife;
        public float AttachDistance;
        public float AttachAngle;
    }

    public struct SocketReturnAnimated : IAnimatedComponent<SocketReturnData>
    {
        public SocketReturnData AuthoredData;
        [CreateProperty] public SocketReturnData Value { get; set; }
    }

    public struct ActiveSocketReturn : IComponentData, IEnableableComponent
    {
        public SocketReturnData Config;
    }

    public struct SocketReturnState : IComponentData
    {
        public float3 LinearVelocity;
        public float3 AngularVelocity;
        public bool Initialized;
    }

    public readonly struct SocketReturnMixer : IMixer<SocketReturnData>
    {
        public SocketReturnData Lerp(in SocketReturnData a, in SocketReturnData b, in float s)
        {
            return new SocketReturnData
            {
                Socket = s < 0.5f ? a.Socket : b.Socket,
                LocalPosition = math.lerp(a.LocalPosition, b.LocalPosition, s),
                LocalRotation = math.slerp(a.LocalRotation, b.LocalRotation, s),
                PositionHalflife = math.lerp(a.PositionHalflife, b.PositionHalflife, s),
                RotationHalflife = math.lerp(a.RotationHalflife, b.RotationHalflife, s),
                AttachDistance = math.lerp(a.AttachDistance, b.AttachDistance, s),
                AttachAngle = math.lerp(a.AttachAngle, b.AttachAngle, s)
            };
        }

        public SocketReturnData Add(in SocketReturnData a, in SocketReturnData b)
        {
            return a;
        }
    }
}
