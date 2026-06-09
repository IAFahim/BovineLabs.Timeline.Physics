using BovineLabs.Core.Authoring.EntityCommands;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Authoring;
using BovineLabs.Timeline.Physics.Sockets;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring.Sockets
{
    public class SocketReturnClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Socket")] public Target socket = Target.Target;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localRotationEuler = Vector3.zero;

        [Header("Damping (seconds to halve the gap)")] [Min(0.001f)]
        public float positionHalflife = 0.08f;

        [Min(0.001f)] public float rotationHalflife = 0.08f;

        [Header("Hand-off Tolerance")] [Min(0f)]
        public float attachDistance = 0.02f;

        [Min(0f)] public float attachAngleDegrees = 3f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            var commands = new BakerCommands(context.Baker, clipEntity);

            var builder = new SocketReturnBuilder
            {
                AuthoredData = new SocketReturnData
                {
                    Socket = socket,
                    LocalPosition = localPosition,
                    LocalRotation = quaternion.Euler(math.radians(localRotationEuler)),
                    PositionHalflife = positionHalflife,
                    RotationHalflife = rotationHalflife,
                    AttachDistance = attachDistance,
                    AttachAngle = math.radians(attachAngleDegrees)
                }
            };
            builder.ApplyTo(ref commands);

            base.Bake(clipEntity, context);
        }
    }
}