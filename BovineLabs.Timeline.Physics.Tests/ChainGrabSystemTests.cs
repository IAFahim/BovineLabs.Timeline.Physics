using BovineLabs.Core.PhysicsStates;
using BovineLabs.Testing;
using BovineLabs.Timeline.Physics.Chains;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace BovineLabs.Timeline.Physics.Tests
{
    public class ChainGrabSystemTests : ECSTestsFixture
    {
        [Test]
        public void Grab_ArmedLink_CreatesJointAndMarksGrabbed()
        {
            var ecbSystem = World.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();

            var root = Manager.CreateEntity();
            Manager.AddBuffer<ChainAnchor>(root);

            var other = CreateWorldEntity(new float3(1f, 0f, 0f));

            var link = Manager.CreateEntity();
            Manager.AddComponentData(link, new ChainLink { Index = 0, Root = root });
            Manager.AddComponentData(link, new ChainGrabConfig { Mode = ChainGrabMode.Stick, HitMask = 0 });
            Manager.AddComponent<ChainGrabArmed>(link);
            Manager.AddComponent<ChainLinkGrabbed>(link);
            Manager.SetComponentEnabled<ChainGrabArmed>(link, true);
            Manager.SetComponentEnabled<ChainLinkGrabbed>(link, false);
            Manager.AddComponentData(link, new LocalToWorld { Value = float4x4.identity });
            Manager.AddBuffer<StatefulCollisionEvent>(link).Add(new StatefulCollisionEvent
            {
                EntityB = other,
                State = StatefulEventState.Enter
            });

            var system = World.GetOrCreateSystem<ChainGrabSystem>();
            system.Update(WorldUnmanaged);
            Manager.CompleteAllTrackedJobs();
            ecbSystem.Update();

            var anchors = Manager.GetBuffer<ChainAnchor>(root);
            Assert.AreEqual(1, anchors.Length);
            Assert.AreEqual(link, anchors[0].Link);
            Assert.AreNotEqual(Entity.Null, anchors[0].Joint);
            Assert.IsTrue(Manager.Exists(anchors[0].Joint));
            Assert.IsTrue(Manager.HasComponent<PhysicsConstrainedBodyPair>(anchors[0].Joint));
            Assert.IsTrue(Manager.IsComponentEnabled<ChainLinkGrabbed>(link));
            Assert.IsFalse(Manager.IsComponentEnabled<ChainGrabArmed>(link));
        }

        private Entity CreateWorldEntity(float3 position)
        {
            var entity = Manager.CreateEntity();
            Manager.AddComponentData(entity, new LocalToWorld
            {
                Value = float4x4.Translate(position)
            });
            return entity;
        }
    }
}
