using BovineLabs.Timeline.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsPIDClip : DOTSClip, ITimelineClipAsset
    {
        [Header("Destination")]
        [Tooltip("0 = Object flies to its own offset position.\n1 = Object chases the Reaction Target entity.\nBlend between the two for partial homing.")]
        [Range(0f, 1f)]
        public float chaseTargetBlend;

        [Tooltip("The world-space offset from the reference point (self or target) that the object tries to reach.\nExample: (0, 0, 10) = 10 units in front.")]
        public Vector3 localTargetOffset = new(0, 0, 10f);

        [Header("PID Tuning")]
        [Tooltip("When enabled, editing any axis of Proportional / Integral / Derivative will set all three axes to the same value.\nUseful for objects that should behave uniformly in all directions.")]
        public bool uniformAxes = true;

        [Tooltip("How hard the object accelerates toward the goal.\n\nToo LOW → feels sluggish, never gets there.\nToo HIGH → oscillates or overshoots badly.\n\nStart here when tuning.")]
        public Vector3 proportional = new(10f, 10f, 10f);

        [Tooltip("Corrects long-term drift — a small push that builds up when the object sits near but not at the goal.\n\nToo LOW → object settles slightly off-target.\nToo HIGH → slow wobble that grows over time.\n\nSet to 0 first; only add if the object never quite arrives.")]
        public Vector3 integral = new(2f, 2f, 2f);

        [Tooltip("Acts as a brake — resists rapid movement to reduce overshoot and oscillation.\n\nToo LOW → object bounces past the goal.\nToo HIGH → object moves extremely slowly.\n\nIncrease this first when you see overshooting.")]
        public Vector3 derivative = new(1f, 1f, 1f);

        [Tooltip("Hard cap on the force applied each frame (in physics force units).\nPrevents the object from teleporting at extreme distances.\nIncrease if it feels like the object 'hits a wall' far from the goal.")]
        public float maxForce = 100f;

        public override double duration => 1;
        public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Looping;

        public void SetProportionalUniform(float v) => proportional = new Vector3(v, v, v);
        public void SetIntegralUniform(float v)     => integral     = new Vector3(v, v, v);
        public void SetDerivativeUniform(float v)   => derivative   = new Vector3(v, v, v);

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsPIDAnimated
            {
                AuthoredData = new PhysicsPIDData
                {
                    Proportional = proportional,
                    Integral     = integral,
                    Derivative   = derivative,
                    LocalTargetOffset = localTargetOffset,
                    ChaseTargetBlend = chaseTargetBlend,
                    MaxForce = maxForce
                }
            });

            base.Bake(clipEntity, context);
        }
    }
}