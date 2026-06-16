using BovineLabs.Reaction.Data.Core;
using BovineLabs.Timeline.Physics.Chains;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace BovineLabs.Timeline.Physics.Authoring.Chains
{
    public class ChainWeaponAuthoring : MonoBehaviour
    {
        [Header("Ordered link bodies (root to tip)")]
        public Transform[] links;

        [Header("Swing-animation bones (parallel to links)")]
        public Transform[] animationBones;

        [Header("Wielder attachment (link 0 fixes here)")]
        public Transform attachBone;

        [Header("On Hit")] public ChainGrabMode grabMode = ChainGrabMode.Wrap;
        public uint hitMask = ~0u;
        public bool enableCollisionWithHit;
        public Target reelAnchor = Target.Self;
        public float reelSpeed = 4f;
        public float reelMinDistance = 0.5f;

        private class ChainWeaponBaker : Baker<ChainWeaponAuthoring>
        {
            public override void Bake(ChainWeaponAuthoring authoring)
            {
                var count = authoring.links != null ? authoring.links.Length : 0;
                if (count == 0) return;

                // A null element in links is dereferenced below (InverseTransformPoint/.position) and would
                // throw an NRE at bake; fail loud and skip so an unassigned slot is a visible authoring error.
                for (var i = 0; i < count; i++)
                {
                    if (authoring.links[i] == null)
                    {
                        UnityEngine.Debug.LogError($"ChainWeaponAuthoring on '{authoring.name}': links[{i}] is unassigned; the chain will not bake.", authoring);
                        return;
                    }
                }

                var root = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(root, new ChainRoot { LinkCount = count });
                AddComponent<ActiveChainFollow>(root);
                SetComponentEnabled<ActiveChainFollow>(root, false);
                AddBuffer<ChainAnchor>(root);
                AddComponent<ChainReleaseRequest>(root);
                SetComponentEnabled<ChainReleaseRequest>(root, false);

                for (var i = 0; i < count; i++)
                {
                    var linkEntity = GetEntity(authoring.links[i], TransformUsageFlags.Dynamic);
                    var bone = authoring.animationBones != null && i < authoring.animationBones.Length
                        ? GetEntity(authoring.animationBones[i], TransformUsageFlags.Dynamic)
                        : Entity.Null;

                    AddComponent(linkEntity, new ChainLink { Index = i, Root = root, AnimationBone = bone });

                    AddComponent(linkEntity, new ChainGrabConfig
                    {
                        Mode = authoring.grabMode,
                        HitMask = authoring.hitMask,
                        EnableCollision = authoring.enableCollisionWithHit,
                        ReelAnchor = authoring.reelAnchor,
                        ReelSpeed = authoring.reelSpeed,
                        ReelMinDistance = authoring.reelMinDistance
                    });
                    AddComponent<ChainGrabArmed>(linkEntity);
                    SetComponentEnabled<ChainGrabArmed>(linkEntity, false);
                    AddComponent<ChainLinkGrabbed>(linkEntity);
                    SetComponentEnabled<ChainLinkGrabbed>(linkEntity, false);
                }

                for (var i = 0; i < count - 1; i++)
                {
                    var a = GetEntity(authoring.links[i], TransformUsageFlags.Dynamic);
                    var b = GetEntity(authoring.links[i + 1], TransformUsageFlags.Dynamic);
                    var anchorA = (float3)authoring.links[i].InverseTransformPoint(authoring.links[i + 1].position);

                    var jointEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, $"ChainJoint_{i}");
                    AddComponent(jointEntity, new PhysicsConstrainedBodyPair(a, b, false));
                    AddComponent(jointEntity, PhysicsJoint.CreateBallAndSocket(anchorA, float3.zero));
                }

                if (authoring.attachBone != null)
                {
                    var attach = GetEntity(authoring.attachBone, TransformUsageFlags.Dynamic);
                    var link0 = GetEntity(authoring.links[0], TransformUsageFlags.Dynamic);
                    var anchorOnLink =
                        (float3)authoring.links[0].InverseTransformPoint(authoring.attachBone.position);

                    var jointEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "ChainAttach");
                    AddComponent(jointEntity, new PhysicsConstrainedBodyPair(link0, attach, false));
                    AddComponent(jointEntity, PhysicsJoint.CreateBallAndSocket(anchorOnLink, float3.zero));
                }
            }
        }
    }
}