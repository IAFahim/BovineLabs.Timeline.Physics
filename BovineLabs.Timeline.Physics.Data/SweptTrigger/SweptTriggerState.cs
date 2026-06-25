using Unity.Entities;
using Unity.Mathematics;

namespace BovineLabs.Timeline.Physics
{
    public struct SweptTriggerState : IComponentData
    {
        public float3 PrevPosition;
        public quaternion PrevRotation;

        public byte WasActive;

        /// <summary>
        /// 1 once a valid previous pose has been recorded (set on the very first tick the source is processed).
        /// Independent of <see cref="WasActive"/> so pose history is continuous — the first damage-active frame
        /// sweeps from the real previous pose instead of a zero-length sweep that tunnels through thin obstacles.
        /// </summary>
        public byte Initialized;
    }
}