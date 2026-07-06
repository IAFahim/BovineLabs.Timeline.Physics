using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Data;
using BovineLabs.Timeline.Physics.Data.Kernels;
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

        // 1 for any authored clip, 0 for the default-fill the blend framework injects into empty slots. An empty
        // slot's zero quaternion slerps to a NON-unit rotation (conjugate != inverse downstream, AttachAngle
        // misfires) and its zero halflives mean infinite stiffness / an unreachable arrival, so the mixer keys off
        // this flag to fall back to the present side's values instead of blending toward the zeroed default.
        public byte Present;
    }

    public struct SocketReturnAnimated : IAnimatedComponent<SocketReturnData>, IPreparable
    {
        public SocketReturnData AuthoredData;
        [CreateProperty] public SocketReturnData Value { get; set; }

        public void ResetToAuthored()
        {
            Value = AuthoredData;
        }
    }

    public struct ActiveSocketReturn : IActive<SocketReturnData>
    {
        public SocketReturnData Config { get; set; }
    }

    public struct SocketReturnState : IComponentData
    {
        public float3 LinearVelocity;
        public float3 AngularVelocity;
        public bool Initialized;
    }

    public readonly struct SocketReturnMixer : IMixer<SocketReturnData>
    {
        private static quaternion SanitizeRotation(in quaternion q)
        {
            return math.lengthsq(q.value) > 1e-6f ? math.normalize(q) : quaternion.identity;
        }

        public SocketReturnData Lerp(in SocketReturnData a, in SocketReturnData b, in float s)
        {
            // Replace an empty slot with the present side so every field blends toward real authored values instead
            // of the zeroed default (zero quaternion -> non-unit slerp, zero halflife -> infinite stiffness, zero
            // attach distance -> unreachable). Rotations are sanitized so a garbage quaternion can never slip through.
            var aData = a.Present == 0 ? b : a;
            var bData = b.Present == 0 ? a : b;

            return new SocketReturnData
            {
                Socket = s < 0.5f ? aData.Socket : bData.Socket,
                LocalPosition = math.lerp(aData.LocalPosition, bData.LocalPosition, s),
                LocalRotation =
                    math.slerp(SanitizeRotation(aData.LocalRotation), SanitizeRotation(bData.LocalRotation), s),
                PositionHalflife = math.lerp(aData.PositionHalflife, bData.PositionHalflife, s),
                RotationHalflife = math.lerp(aData.RotationHalflife, bData.RotationHalflife, s),
                AttachDistance = math.lerp(aData.AttachDistance, bData.AttachDistance, s),
                AttachAngle = math.lerp(aData.AttachAngle, bData.AttachAngle, s),
                Present = (byte)(a.Present | b.Present)
            };
        }

        public SocketReturnData Add(in SocketReturnData a, in SocketReturnData b)
        {
            return a.Present != 0 ? a : b;
        }
    }
}