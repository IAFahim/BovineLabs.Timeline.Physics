using BovineLabs.Timeline.Authoring;
using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Timeline;

namespace BovineLabs.Timeline.Physics.Authoring
{
    public class PhysicsVelocityClip : DOTSClip, ITimelineClipAsset
    {
        [SerializeField] [Tooltip("Linear velocity in world units per second")]
        private Vector3 linearVelocity = Vector3.forward;

        [SerializeField] [Tooltip("Angular velocity in radians per second")]
        private Vector3 angularVelocity;

        [SerializeField] private bool isLocalSpace;

        public float3 LinearVelocity => linearVelocity;
        public float3 AngularVelocity => angularVelocity;

        public Target lookAt;

        public override double duration => 1;

        public ClipCaps clipCaps => ClipCaps.Looping;

        public override void Bake(Entity clipEntity, BakingContext context)
        {
            context.Baker.AddComponent(clipEntity, new PhysicsVelocityComponent
            {
                PhysicsVelocity = new PhysicsVelocity
                {
                    Linear = LinearVelocity,
                    Angular = AngularVelocity
                },
                IsLocalSpace = isLocalSpace,
                Target = lookAt
            });
            base.Bake(clipEntity, context);
        }
    }
}
